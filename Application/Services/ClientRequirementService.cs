using Application.DTOs.ClientRequirement;
using Application.Exceptions;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

namespace Application.Services
{
    public class ClientRequirementService
    {
        private readonly IClientRequirementRepository _repository;
        private readonly IClientRepository _clientRepository;
        private readonly IPropertyRepository _propertyRepository;
        private readonly IPropertyMatchNotificationLogRepository _notificationLogRepository;
        private readonly PropertyMatchingNotificationService _notificationService;
        private readonly IMapper _mapper;

        public ClientRequirementService(
            IClientRequirementRepository repository,
            IClientRepository clientRepository,
            IPropertyRepository propertyRepository,
            IPropertyMatchNotificationLogRepository notificationLogRepository,
            PropertyMatchingNotificationService notificationService,
            IMapper mapper)
        {
            _repository = repository;
            _clientRepository = clientRepository;
            _propertyRepository = propertyRepository;
            _notificationLogRepository = notificationLogRepository;
            _notificationService = notificationService;
            _mapper = mapper;
        }

        public async Task<RequirementResponse> Create(CreateRequirementRequest request)
        {
            var client = await _clientRepository.GetById(request.ClientId);
            if (client is null)
                throw new NotFoundException("Клиент не найден.");

            var existing = await _repository.GetActiveByClient(request.ClientId);
            if (existing is not null)
                throw new ConflictException("У клиента уже есть активное требование.");

            var requirement = _mapper.Map<ClientRequirement>(request);
            await _repository.Add(requirement);

            return _mapper.Map<RequirementResponse>(requirement);
        }

        public async Task<RequirementResponse> GetById(Guid id)
        {
            var requirement = await _repository.GetById(id);
            if (requirement is null)
                throw new NotFoundException("Требование не найдено.");

            return _mapper.Map<RequirementResponse>(requirement);
        }

        public async Task<List<RequirementResponse>> Get(int limit)
        {
            var list = await _repository.Get(limit);
            return _mapper.Map<List<RequirementResponse>>(list);
        }

        public async Task<RequirementResponse?> GetActive(Guid clientId)
        {
            var requirement = await _repository.GetActiveByClient(clientId);
            return requirement is null
                ? null
                : _mapper.Map<RequirementResponse>(requirement);
        }

        public async Task<Guid> ResolveClientIdByEmail(string email)
        {
            var normalizedEmail = email.Trim();
            if (string.IsNullOrWhiteSpace(normalizedEmail))
                throw new NotFoundException("Email пользователя не найден в токене.");

            var client = await _clientRepository.GetByEmail(normalizedEmail);
            if (client is null)
                throw new NotFoundException("Профиль клиента для текущего пользователя не найден. Войдите под клиентским аккаунтом или создайте профиль клиента.");

            return client.Id;
        }

        public async Task<RequirementResponse> Update(UpdateRequirementRequest request)
        {
            var requirement = await _repository.GetById(request.Id);
            if (requirement is null)
                throw new NotFoundException("Требование не найдено.");

            requirement.Update(
                request.DesiredType,
                new Location(request.Latitude, request.Longitude),
                request.SearchRadiusMeters,
                request.MinPrice,
                request.MaxPrice,
                request.MinArea,
                request.MinMatchPercentage,
                request.PriceWeight,
                request.AreaWeight,
                request.Criteria is null
                    ? null
                    : request.Criteria
                        .Select(x => _mapper.Map<ClientRequirementCriterion>(x))
                        .ToList(),
                request.DesiredTypes,
                request.MaxArea,
                request.AddressQuery,
                request.IgnoreArea);

            _repository.Update(requirement);
            return _mapper.Map<RequirementResponse>(requirement);
        }

        public async Task<int> Subscribe(Guid requirementId, int topMatchesLimit = 5)
        {
            var requirement = await _repository.GetById(requirementId);
            if (requirement is null)
                throw new NotFoundException("Требование не найдено.");

            var client = await _clientRepository.GetById(requirement.ClientId);
            return await _notificationService.SubscribeRequirement(requirement, client?.Email, topMatchesLimit);
        }

        public async Task<IReadOnlyList<RequirementNotificationHistoryItemResponse>> GetNotificationHistory(
            Guid requirementId,
            int limit = 20)
        {
            var safeLimit = Math.Clamp(limit, 1, 100);
            var logs = await _notificationLogRepository.GetByRequirement(
                requirementId,
                safeLimit);

            if (logs.Count == 0)
            {
                return [];
            }

            var propertyIds = logs.Select(x => x.PropertyId).Distinct().ToList();
            var properties = await _propertyRepository.GetByIds(propertyIds);
            var propertiesById = properties.ToDictionary(x => x.Id);

            return logs
                .Select(log =>
                {
                    var notificationTypeTitle = GetNotificationTypeTitle(log.NotificationType);

                    if (propertiesById.TryGetValue(log.PropertyId, out var property))
                    {
                        return new RequirementNotificationHistoryItemResponse(
                            log.PropertyId,
                            property.Title,
                            property.Address,
                            property.Price,
                            property.Area,
                            log.SentAt,
                            notificationTypeTitle);
                    }

                    return new RequirementNotificationHistoryItemResponse(
                        log.PropertyId,
                        "Объект недоступен",
                        "Объект был удален или скрыт.",
                        0m,
                        0d,
                        log.SentAt,
                        notificationTypeTitle);
                })
                .ToList();
        }

        private static string GetNotificationTypeTitle(PropertyMatchNotificationType notificationType)
        {
            return notificationType == PropertyMatchNotificationType.NewRelevantProperty
                ? "Новый подходящий объект"
                : "Первичная подборка";
        }

        public async Task Deactivate(Guid id)
        {
            var requirement = await _repository.GetById(id);
            if (requirement is null)
                throw new NotFoundException("Требование не найдено.");

            requirement.Deactivate();
            _repository.Update(requirement);
        }

        public async Task Delete(Guid id)
        {
            var requirement = await _repository.GetById(id);
            if (requirement is null)
                throw new NotFoundException("Требование не найдено.");

            _repository.Delete(requirement);
        }
    }
}
