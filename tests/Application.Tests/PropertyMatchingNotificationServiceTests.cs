using Application.DTOs.PropertyMatching;
using Application.Email;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

namespace Application.Tests;

public class PropertyMatchingNotificationServiceTests
{
    [Fact]
    public async Task NotifyTopMatchesForRequirement_ShouldSendTopMatchesEmail()
    {
        var client = CreateClient("client@example.com");
        var requirement = CreateRequirement(client.Id, minMatchPercentage: 0.1);
        var property = CreateProperty();

        var propertyRepository = new FakePropertyRepository([property]);
        var requirementRepository = new FakeClientRequirementRepository(requirement);
        var matchingService = new PropertyMatchingService(propertyRepository, requirementRepository);
        var logRepository = new FakePropertyMatchNotificationLogRepository();
        var emailSender = new FakeEmailSender();
        var applicationEmailService = new ApplicationEmailService(emailSender);
        var clientRepository = new FakeClientRepository([client]);

        var notificationService = new PropertyMatchingNotificationService(
            matchingService,
            logRepository,
            clientRepository,
            applicationEmailService);

        await notificationService.NotifyTopMatchesForRequirement(requirement, client.Email, topMatchesLimit: 5);

        var email = Assert.Single(emailSender.SentMessages);
        Assert.Contains("Найдены подходящие варианты недвижимости", email.Subject);
        Assert.Contains("Совпадение:", email.Body);
        Assert.Contains("Расстояние:", email.Body);
        Assert.Single(logRepository.Logs);
    }

    [Fact]
    public async Task NotifyTopMatchesForRequirement_ShouldNotResendDuplicateMatch()
    {
        var client = CreateClient("client@example.com");
        var requirement = CreateRequirement(client.Id, minMatchPercentage: 0.1);
        var property = CreateProperty();

        var existingLog = new PropertyMatchNotificationLog(
            requirement.Id,
            property.Id,
            PropertyMatchNotificationType.RequirementTopMatches);

        var propertyRepository = new FakePropertyRepository([property]);
        var requirementRepository = new FakeClientRequirementRepository(requirement);
        var matchingService = new PropertyMatchingService(propertyRepository, requirementRepository);
        var logRepository = new FakePropertyMatchNotificationLogRepository([existingLog]);
        var emailSender = new FakeEmailSender();
        var applicationEmailService = new ApplicationEmailService(emailSender);
        var clientRepository = new FakeClientRepository([client]);

        var notificationService = new PropertyMatchingNotificationService(
            matchingService,
            logRepository,
            clientRepository,
            applicationEmailService);

        await notificationService.NotifyTopMatchesForRequirement(requirement, client.Email, topMatchesLimit: 5);

        Assert.Empty(emailSender.SentMessages);
        Assert.Single(logRepository.Logs);
    }

    [Fact]
    public async Task NotifySubscribedClientsForProperty_ShouldSendEmailForMatchingRequirement()
    {
        var client = CreateClient("client@example.com");
        var requirement = CreateRequirement(client.Id, minMatchPercentage: 0.1);
        var property = CreateProperty();

        var propertyRepository = new FakePropertyRepository([property]);
        var requirementRepository = new FakeClientRequirementRepository(requirement);
        var matchingService = new PropertyMatchingService(propertyRepository, requirementRepository);
        var subscribedLog = new PropertyMatchNotificationLog(
            requirement.Id,
            Guid.NewGuid(),
            PropertyMatchNotificationType.RequirementTopMatches);
        var logRepository = new FakePropertyMatchNotificationLogRepository([subscribedLog]);
        var emailSender = new FakeEmailSender();
        var applicationEmailService = new ApplicationEmailService(emailSender);
        var clientRepository = new FakeClientRepository([client]);

        var notificationService = new PropertyMatchingNotificationService(
            matchingService,
            logRepository,
            clientRepository,
            applicationEmailService);

        await notificationService.NotifySubscribedClientsForProperty(property);

        var email = Assert.Single(emailSender.SentMessages);
        Assert.Contains("Новый подходящий объект", email.Subject);
        Assert.Contains(property.Address, email.Body);
        var log = Assert.Single(logRepository.Logs, x =>
            x.NotificationType == PropertyMatchNotificationType.NewRelevantProperty
            && x.PropertyId == property.Id);
        Assert.Equal(PropertyMatchNotificationType.NewRelevantProperty, log.NotificationType);
    }

    [Fact]
    public async Task NotifySubscribedClientsForProperty_ShouldNotSendPropertyFromInitialTopMatches()
    {
        var client = CreateClient("client@example.com");
        var requirement = CreateRequirement(client.Id, minMatchPercentage: 0.1);
        var property = CreateProperty();

        var existingLog = new PropertyMatchNotificationLog(
            requirement.Id,
            property.Id,
            PropertyMatchNotificationType.RequirementTopMatches);

        var propertyRepository = new FakePropertyRepository([property]);
        var requirementRepository = new FakeClientRequirementRepository(requirement);
        var matchingService = new PropertyMatchingService(propertyRepository, requirementRepository);
        var logRepository = new FakePropertyMatchNotificationLogRepository([existingLog]);
        var emailSender = new FakeEmailSender();
        var applicationEmailService = new ApplicationEmailService(emailSender);
        var clientRepository = new FakeClientRepository([client]);

        var notificationService = new PropertyMatchingNotificationService(
            matchingService,
            logRepository,
            clientRepository,
            applicationEmailService);

        var emailsSent = await notificationService.NotifySubscribedClientsForProperty(property);

        Assert.Equal(0, emailsSent);
        Assert.Empty(emailSender.SentMessages);
        Assert.Single(logRepository.Logs);
    }

    private static Client CreateClient(string? email)
    {
        return new Client(
            new FullName("Иван", "Клиент", null),
            "+37360000000",
            email);
    }

    private static ClientRequirement CreateRequirement(Guid clientId, double minMatchPercentage)
    {
        return new ClientRequirement(
            clientId: clientId,
            desiredType: PropertyType.Apartment,
            targetLocation: new Location(47.0105, 28.8638),
            searchRadiusMeters: 5_000,
            minPrice: 100_000m,
            maxPrice: 250_000m,
            minArea: 50,
            minMatchPercentage: minMatchPercentage,
            priceWeight: 0.6,
            areaWeight: 0.4);
    }

    private static Property CreateProperty()
    {
        return new Property(
            title: "Central apartment",
            address: "Str. Stefan cel Mare 10",
            price: 150_000m,
            area: 60,
            roomsCount: 2,
            location: new Location(47.0110, 28.8640),
            type: PropertyType.Apartment);
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public List<EmailMessage> SentMessages { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            SentMessages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FakePropertyMatchNotificationLogRepository : IPropertyMatchNotificationLogRepository
    {
        private readonly List<PropertyMatchNotificationLog> _logs;

        public FakePropertyMatchNotificationLogRepository(List<PropertyMatchNotificationLog>? logs = null)
        {
            _logs = logs ?? [];
        }

        public IReadOnlyCollection<PropertyMatchNotificationLog> Logs => _logs.AsReadOnly();

        public Task<HashSet<Guid>> GetSentPropertyIds(
            Guid requirementId,
            PropertyMatchNotificationType notificationType,
            IReadOnlyCollection<Guid> propertyIds)
        {
            var sentIds = _logs
                .Where(x => x.RequirementId == requirementId
                            && x.NotificationType == notificationType
                            && propertyIds.Contains(x.PropertyId))
                .Select(x => x.PropertyId)
                .ToHashSet();

            return Task.FromResult(sentIds);
        }

        public Task<HashSet<Guid>> GetSentRequirementIds(
            Guid propertyId,
            PropertyMatchNotificationType notificationType,
            IReadOnlyCollection<Guid> requirementIds)
        {
            var sentIds = _logs
                .Where(x => x.PropertyId == propertyId
                            && x.NotificationType == notificationType
                            && requirementIds.Contains(x.RequirementId))
                .Select(x => x.RequirementId)
                .ToHashSet();

            return Task.FromResult(sentIds);
        }

        public Task<HashSet<Guid>> GetSentRequirementIds(
            Guid propertyId,
            IReadOnlyCollection<Guid> requirementIds)
        {
            var sentIds = _logs
                .Where(x => x.PropertyId == propertyId
                            && requirementIds.Contains(x.RequirementId))
                .Select(x => x.RequirementId)
                .ToHashSet();

            return Task.FromResult(sentIds);
        }

        public Task<List<PropertyMatchNotificationLog>> GetByRequirement(
            Guid requirementId,
            PropertyMatchNotificationType notificationType,
            int limit)
        {
            var items = _logs
                .Where(x => x.RequirementId == requirementId && x.NotificationType == notificationType)
                .OrderByDescending(x => x.SentAt)
                .Take(limit)
                .ToList();

            return Task.FromResult(items);
        }

        public Task<List<PropertyMatchNotificationLog>> GetByRequirement(
            Guid requirementId,
            int limit)
        {
            var items = _logs
                .Where(x => x.RequirementId == requirementId)
                .OrderByDescending(x => x.SentAt)
                .Take(limit)
                .ToList();

            return Task.FromResult(items);
        }

        public Task<HashSet<Guid>> GetRequirementIdsWithNotificationType(
            IReadOnlyCollection<Guid> requirementIds,
            PropertyMatchNotificationType notificationType)
        {
            var ids = _logs
                .Where(x => requirementIds.Contains(x.RequirementId) && x.NotificationType == notificationType)
                .Select(x => x.RequirementId)
                .ToHashSet();

            return Task.FromResult(ids);
        }

        public Task AddRange(IReadOnlyCollection<PropertyMatchNotificationLog> logs)
        {
            _logs.AddRange(logs);
            return Task.CompletedTask;
        }

        public Task<PropertyMatchNotificationLog?> GetById(Guid id)
        {
            return Task.FromResult(_logs.FirstOrDefault(x => x.Id == id));
        }

        public Task<List<PropertyMatchNotificationLog>> Get(int limit)
        {
            return Task.FromResult(_logs.Take(limit).ToList());
        }

        public Task<PropertyMatchNotificationLog> Add(PropertyMatchNotificationLog entity)
        {
            _logs.Add(entity);
            return Task.FromResult(entity);
        }

        public void Update(PropertyMatchNotificationLog entity)
        {
        }

        public bool Delete(PropertyMatchNotificationLog entity)
        {
            return _logs.Remove(entity);
        }
    }

    private sealed class FakeClientRepository : IClientRepository
    {
        private readonly List<Client> _clients;

        public FakeClientRepository(List<Client> clients)
        {
            _clients = clients;
        }

        public Task<Client?> GetById(Guid id)
        {
            return Task.FromResult(_clients.FirstOrDefault(x => x.Id == id));
        }

        public Task<List<Client>> Get(int limit)
        {
            return Task.FromResult(_clients.Take(limit).ToList());
        }

        public Task<Client> Add(Client entity)
        {
            _clients.Add(entity);
            return Task.FromResult(entity);
        }

        public void Update(Client entity)
        {
        }

        public bool Delete(Client entity)
        {
            return _clients.Remove(entity);
        }

        public Task<Client?> GetByPhone(string phone)
        {
            return Task.FromResult(_clients.FirstOrDefault(x => x.PhoneNumber == phone));
        }

        public Task<Client?> GetByEmail(string email)
        {
            return Task.FromResult(_clients.FirstOrDefault(x =>
                string.Equals(x.Email, email, StringComparison.OrdinalIgnoreCase)));
        }
    }

    private sealed class FakePropertyRepository : IPropertyRepository
    {
        private readonly List<Property> _properties;

        public FakePropertyRepository(List<Property> properties)
        {
            _properties = properties;
        }

        public Task<Property?> GetById(Guid id)
        {
            return Task.FromResult(_properties.FirstOrDefault(x => x.Id == id));
        }

        public Task<List<Property>> Get(int limit)
        {
            return Task.FromResult(_properties.Take(limit).ToList());
        }

        public Task<Property> Add(Property entity)
        {
            _properties.Add(entity);
            return Task.FromResult(entity);
        }

        public void Update(Property entity)
        {
        }

        public bool Delete(Property entity)
        {
            return _properties.Remove(entity);
        }

        public Task<List<Property>> GetAvailable()
        {
            var available = _properties
                .Where(x => x.Status == PropertyStatus.Available)
                .ToList();

            return Task.FromResult(available);
        }

        public Task<List<Property>> GetByResponsibleRealtor(Guid realtorId, DateTime fromUtc)
        {
            var items = _properties
                .Where(x => x.ResponsibleRealtorId == realtorId)
                .Where(x => x.CreatedDate >= fromUtc)
                .ToList();

            return Task.FromResult(items);
        }

        public Task<List<Property>> GetByIds(IReadOnlyCollection<Guid> ids)
        {
            var items = _properties.Where(x => ids.Contains(x.Id)).ToList();
            return Task.FromResult(items);
        }

        public Task<Property?> GetByIdForUpdateWithCriteria(Guid id)
        {
            return GetById(id);
        }

        public void AddCriterionValue(PropertyCriterionValue value)
        {
        }

        public Task<List<PropertyMatchCandidate>> GetCandidatesForRequirement(
            ClientRequirement requirement,
            int take)
        {
            var candidates = _properties
                .Take(take)
                .Select(x => new PropertyMatchCandidate(x, 250.0))
                .ToList();

            return Task.FromResult(candidates);
        }
    }

    private sealed class FakeClientRequirementRepository : IClientRequirementRepository
    {
        private readonly ClientRequirement _requirement;

        public FakeClientRequirementRepository(ClientRequirement requirement)
        {
            _requirement = requirement;
        }

        public Task<ClientRequirement?> GetById(Guid id)
        {
            return Task.FromResult(_requirement.Id == id ? _requirement : null);
        }

        public Task<List<ClientRequirement>> Get(int limit)
        {
            return Task.FromResult(new List<ClientRequirement> { _requirement }.Take(limit).ToList());
        }

        public Task<ClientRequirement> Add(ClientRequirement entity)
        {
            return Task.FromResult(entity);
        }

        public void Update(ClientRequirement entity)
        {
        }

        public bool Delete(ClientRequirement entity)
        {
            return true;
        }

        public Task<ClientRequirement?> GetActiveByClient(Guid clientId)
        {
            if (_requirement.ClientId == clientId && _requirement.IsActive)
                return Task.FromResult<ClientRequirement?>(_requirement);

            return Task.FromResult<ClientRequirement?>(null);
        }

        public Task<List<ClientRequirement>> GetActive(int limit)
        {
            var active = _requirement.IsActive
                ? new List<ClientRequirement> { _requirement }.Take(limit).ToList()
                : [];

            return Task.FromResult(active);
        }
    }
}
