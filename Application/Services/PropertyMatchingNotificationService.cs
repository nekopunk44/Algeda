using Application.DTOs.PropertyMatching;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Application.Services
{
    public class PropertyMatchingNotificationService
    {
        private const int DefaultTopMatchesLimit = 5;
        private const int DefaultRequirementsLimit = 500;

        private readonly PropertyMatchingService _matchingService;
        private readonly IPropertyMatchNotificationLogRepository _notificationLogRepository;
        private readonly IClientRepository _clientRepository;
        private readonly IApplicationEmailService _applicationEmailService;

        public PropertyMatchingNotificationService(
            PropertyMatchingService matchingService,
            IPropertyMatchNotificationLogRepository notificationLogRepository,
            IClientRepository clientRepository,
            IApplicationEmailService applicationEmailService)
        {
            _matchingService = matchingService;
            _notificationLogRepository = notificationLogRepository;
            _clientRepository = clientRepository;
            _applicationEmailService = applicationEmailService;
        }

        public async Task<int> NotifyTopMatchesForRequirement(
            ClientRequirement requirement,
            string? clientEmail,
            int topMatchesLimit = DefaultTopMatchesLimit)
        {
            if (!requirement.IsActive)
                return 0;

            if (string.IsNullOrWhiteSpace(clientEmail))
                return 0;

            try
            {
                var matches = await _matchingService.FindMatches(new PropertyMatchingRequest(
                    requirement.Id,
                    Math.Max(1, topMatchesLimit)));

                if (matches.Count == 0)
                    return 0;

                var propertyIds = matches
                    .Select(x => x.PropertyId)
                    .ToList();

                var sentPropertyIds = await _notificationLogRepository.GetSentPropertyIds(
                    requirement.Id,
                    PropertyMatchNotificationType.RequirementTopMatches,
                    propertyIds);

                var freshMatches = matches
                    .Where(x => !sentPropertyIds.Contains(x.PropertyId))
                    .ToList();

                if (freshMatches.Count == 0)
                    return 0;

                await _applicationEmailService.SendRequirementTopMatchesAsync(
                    clientEmail,
                    requirement,
                    freshMatches);

                var logs = freshMatches
                    .Select(x => new PropertyMatchNotificationLog(
                        requirement.Id,
                        x.PropertyId,
                        PropertyMatchNotificationType.RequirementTopMatches))
                    .ToList();

                await TryPersistLogs(logs);
                return 1;
            }
            catch
            {
                return 0;
            }
        }

        public async Task<int> SubscribeRequirement(
            ClientRequirement requirement,
            string? clientEmail,
            int topMatchesLimit = DefaultTopMatchesLimit)
        {
            return await NotifyTopMatchesForRequirement(requirement, clientEmail, topMatchesLimit);
        }

        public async Task<int> NotifySubscribedClientsForProperty(
            Property property,
            int requirementsLimit = DefaultRequirementsLimit)
        {
            if (property.Status != PropertyStatus.Available)
                return 0;

            try
            {
                var matches = await _matchingService.FindRequirementMatchesForProperty(
                    property.Id,
                    requirementsLimit);

                if (matches.Count == 0)
                    return 0;

                var requirementIds = matches
                    .Select(x => x.RequirementId)
                    .Distinct()
                    .ToList();

                var subscribedRequirementIds = await _notificationLogRepository.GetRequirementIdsWithNotificationType(
                    requirementIds,
                    PropertyMatchNotificationType.RequirementTopMatches);

                if (subscribedRequirementIds.Count == 0)
                    return 0;

                var subscribedMatches = matches
                    .Where(x => subscribedRequirementIds.Contains(x.RequirementId))
                    .ToList();

                if (subscribedMatches.Count == 0)
                    return 0;

                var subscribedRequirementIdsForDedup = subscribedMatches
                    .Select(x => x.RequirementId)
                    .ToList();

                var sentRequirementIds = await _notificationLogRepository.GetSentRequirementIds(
                    property.Id,
                    subscribedRequirementIdsForDedup);

                var freshMatches = subscribedMatches
                    .Where(x => !sentRequirementIds.Contains(x.RequirementId))
                    .ToList();

                if (freshMatches.Count == 0)
                    return 0;

                var logs = new List<PropertyMatchNotificationLog>();
                var emailsSent = 0;

                foreach (var match in freshMatches)
                {
                    var client = await _clientRepository.GetById(match.ClientId);
                    var email = client?.Email?.Trim();

                    if (string.IsNullOrWhiteSpace(email))
                        continue;

                    try
                    {
                        await _applicationEmailService.SendNewRelevantPropertyAsync(email, match);

                        logs.Add(new PropertyMatchNotificationLog(
                            match.RequirementId,
                            match.PropertyId,
                            PropertyMatchNotificationType.NewRelevantProperty));
                        emailsSent++;
                    }
                    catch
                    {
                        // Один сбой при отправке письма не должен останавливать рассылку другим клиентам.
                    }
                }

                await TryPersistLogs(logs);
                return emailsSent;
            }
            catch
            {
                // Сбои в системе уведомлений не должны нарушать основной сценарий API.
                return 0;
            }
        }

        private async Task TryPersistLogs(IReadOnlyCollection<PropertyMatchNotificationLog> logs)
        {
            if (logs.Count == 0)
                return;

            try
            {
                await _notificationLogRepository.AddRange(logs);
            }
            catch
            {
                // Уникальный индекс защищает лог от дублей при параллельных проверках.
            }
        }
    }
}
