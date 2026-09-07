using Application.DTOs.RealtorEfficiency;
using Application.Exceptions;
using Application.Interfaces;
using Application.Mappers;
using Application.Services;
using AutoMapper;
using Domain.Common;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.Tests;

public class RealtorFeedbackServiceTests
{
    [Fact]
    public async Task SubmitForCurrentClient_ShouldReject_WhenFeedbackAlreadyExistsForDeal()
    {
        var fixture = new FeedbackFixture();
        var request = fixture.BuildPurchaseRequest();

        await fixture.Service.SubmitForCurrentClient(fixture.Client.Email!, request);

        await Assert.ThrowsAsync<ConflictException>(async () =>
            await fixture.Service.SubmitForCurrentClient(fixture.Client.Email!, request));
    }

    [Fact]
    public async Task SubmitForCurrentClient_ShouldReject_WhenFeedbackWindowExpired()
    {
        var fixture = new FeedbackFixture(completedDaysAgo: 20);

        var ex = await Assert.ThrowsAsync<ValidationException>(async () =>
            await fixture.Service.SubmitForCurrentClient(fixture.Client.Email!, fixture.BuildPurchaseRequest()));

        Assert.Contains("истек", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitForCurrentClient_ShouldValidatePurchaseAccuracyFields()
    {
        var fixture = new FeedbackFixture();
        var invalid = fixture.BuildPurchaseRequest() with
        {
            TitleAccuracyScore = null,
            CriteriaAccuracyScore = null
        };

        await Assert.ThrowsAsync<DomainException>(async () =>
            await fixture.Service.SubmitForCurrentClient(fixture.Client.Email!, invalid));
    }

    [Fact]
    public async Task SubmitForCurrentClient_ShouldAllowSaleWithoutPropertyAccuracyAnswers()
    {
        var fixture = new FeedbackFixture(source: DealSource.Sale);
        var request = new SubmitDealFeedbackRequest(
            DealId: fixture.Deal.Id,
            ServiceScore: 5,
            FormType: FeedbackFormType.Sale,
            CommunicationScore: 5,
            ResponsivenessScore: 4,
            ExpertiseScore: 5,
            TitleAccuracyScore: null,
            CriteriaAccuracyScore: null,
            DescriptionAccuracyScore: null,
            PhotosAccuracyScore: null,
            Comment: "Продажа прошла хорошо");

        var response = await fixture.Service.SubmitForCurrentClient(fixture.Client.Email!, request);

        Assert.Equal(FeedbackFormType.Sale, response.FormType);
        Assert.Null(response.TitleAccuracyScore);
        Assert.Null(response.CriteriaAccuracyScore);
    }

    [Fact]
    public async Task SubmitForCurrentClient_ShouldReject_WhenDealBelongsToAnotherClient()
    {
        var fixture = new FeedbackFixture();

        await Assert.ThrowsAsync<NotFoundException>(async () =>
            await fixture.Service.SubmitForCurrentClient("other-client@example.com", fixture.BuildPurchaseRequest()));
    }

    private sealed class FeedbackFixture
    {
        private readonly FakeRealtorFeedbackRepository _feedbackRepository = new();
        private readonly FakeDealRepository _dealRepository = new();
        private readonly FakePropertyRepository _propertyRepository = new();
        private readonly FakeClientRepository _clientRepository = new();

        public Client Client { get; }
        public Deal Deal { get; }
        public RealtorFeedbackService Service { get; }

        public FeedbackFixture(DealSource source = DealSource.Matching, int completedDaysAgo = 1)
        {
            Client = new Client(new FullName("Ivan", "Client", null), "+37360000001", "client@example.com");
            var otherClient = new Client(new FullName("Olga", "Other", null), "+37360000002", "other-client@example.com");

            var property = new Property(
                title: "Apartment",
                address: "Main st",
                price: 100000m,
                area: 80,
                roomsCount: 3,
                location: new Location(47.0, 28.0),
                type: PropertyType.Apartment,
                ownerFullName: "Owner Name",
                ownerEmail: "owner@example.com",
                ownerPhoneNumber: "+37360000003");
            property.AssignResponsibleRealtor(Guid.Parse("10000000-0000-0000-0000-000000000111"));

            Deal = source == DealSource.Sale
                ? Deal.CreateIncoming(Client.Id, DealSource.Sale, property.Id, null, "sale")
                : Deal.CreateIncoming(Client.Id, DealSource.Matching, property.Id, Guid.NewGuid(), "purchase");

            Deal.AcceptIncoming(Guid.Parse("10000000-0000-0000-0000-000000000222"));
            Deal.Complete(1000m, "USD");
            SetCompletedAt(Deal, DateTime.UtcNow.AddDays(-completedDaysAgo));

            _dealRepository.AddInMemory(Deal);
            _propertyRepository.AddInMemory(property);
            _clientRepository.AddInMemory(Client);
            _clientRepository.AddInMemory(otherClient);

            var mapper = new MapperConfiguration(
                cfg => cfg.AddProfile<RealtorEfficiencyProfile>(),
                NullLoggerFactory.Instance).CreateMapper();
            Service = new RealtorFeedbackService(
                _feedbackRepository,
                _dealRepository,
                _propertyRepository,
                _clientRepository,
                mapper);
        }

        public SubmitDealFeedbackRequest BuildPurchaseRequest()
        {
            return new SubmitDealFeedbackRequest(
                DealId: Deal.Id,
                ServiceScore: 4,
                FormType: FeedbackFormType.Purchase,
                CommunicationScore: 5,
                ResponsivenessScore: 4,
                ExpertiseScore: 4,
                TitleAccuracyScore: 5,
                CriteriaAccuracyScore: 3,
                DescriptionAccuracyScore: 5,
                PhotosAccuracyScore: 4,
                Comment: "Все хорошо");
        }

        private static void SetCompletedAt(Deal deal, DateTime completedAtUtc)
        {
            var prop = typeof(Deal).GetProperty(nameof(Deal.CompletedAt));
            prop!.SetValue(deal, completedAtUtc);
        }
    }

    private sealed class FakeRealtorFeedbackRepository : IRealtorFeedbackRepository
    {
        private readonly List<RealtorFeedback> _storage = [];

        public Task<RealtorFeedback?> GetByDeal(Guid dealId)
            => Task.FromResult(_storage.FirstOrDefault(x => x.DealId == dealId));

        public Task<List<RealtorFeedback>> GetByServiceRealtor(Guid realtorId, int limit = 100)
            => Task.FromResult(_storage.Where(x => x.ServiceRealtorId == realtorId).Take(limit).ToList());

        public Task<List<RealtorFeedback>> GetByPropertyResponsibleRealtor(Guid realtorId, int limit = 100)
            => Task.FromResult(_storage.Where(x => x.PropertyResponsibleRealtorId == realtorId).Take(limit).ToList());

        public Task<List<RealtorFeedback>> GetByServiceRealtor(Guid realtorId, DateTime fromUtc)
            => Task.FromResult(_storage.Where(x => x.ServiceRealtorId == realtorId && x.CreatedDate >= fromUtc).ToList());

        public Task<List<RealtorFeedback>> GetByPropertyResponsibleRealtor(Guid realtorId, DateTime fromUtc)
            => Task.FromResult(_storage.Where(x => x.PropertyResponsibleRealtorId == realtorId && x.CreatedDate >= fromUtc).ToList());

        public Task<RealtorFeedback?> GetById(Guid id) => Task.FromResult(_storage.FirstOrDefault(x => x.Id == id));

        public Task<List<RealtorFeedback>> Get(int limit) => Task.FromResult(_storage.Take(limit).ToList());

        public Task<RealtorFeedback> Add(RealtorFeedback entity)
        {
            _storage.Add(entity);
            return Task.FromResult(entity);
        }

        public void Update(RealtorFeedback entity)
        {
        }

        public bool Delete(RealtorFeedback entity) => _storage.Remove(entity);
    }

    private sealed class FakeDealRepository : IDealRepository
    {
        private readonly List<Deal> _storage = [];

        public void AddInMemory(Deal deal) => _storage.Add(deal);

        public Task<TResult> ExecuteWorkflow<TResult>(Guid dealId, Func<Task<TResult>> operation) => operation();

        public Task<Deal?> GetById(Guid id) => Task.FromResult(_storage.FirstOrDefault(x => x.Id == id));

        public Task<List<Deal>> Get(int limit) => Task.FromResult(_storage.Take(limit).ToList());

        public Task<Deal> Add(Deal entity)
        {
            _storage.Add(entity);
            return Task.FromResult(entity);
        }

        public void Update(Deal entity)
        {
        }

        public bool Delete(Deal entity) => _storage.Remove(entity);

        public Task<Deal?> GetByIdWithNotes(Guid id) => GetById(id);
        public Task<List<Deal>> GetIncoming(int limit) => Task.FromResult(new List<Deal>());
        public Task<List<Deal>> GetIncomingOrAssignedToRealtor(Guid realtorId, int limit) => Task.FromResult(new List<Deal>());
        public Task<List<Deal>> GetByRealtor(Guid realtorId) => Task.FromResult(new List<Deal>());
        public Task<List<Deal>> GetByClient(Guid clientId) => Task.FromResult(_storage.Where(x => x.ClientId == clientId).ToList());
        public Task<List<Deal>> GetByProperty(Guid propertyId) => Task.FromResult(_storage.Where(x => x.PropertyId == propertyId).ToList());
        public Task<Deal?> GetLatestSaleRequestByProperty(Guid propertyId) => Task.FromResult<Deal?>(null);
        public Task<List<Deal>> GetCompletedByRealtor(Guid realtorId, DateTime fromUtc) => Task.FromResult(new List<Deal>());
        public Task<DealNote?> AddNote(Guid dealId, string text, Guid? authorRealtorId) => Task.FromResult<DealNote?>(null);
        public Task<DealNote?> UpdateNote(Guid dealId, Guid noteId, string text) => Task.FromResult<DealNote?>(null);
        public Task<bool> DeleteNote(Guid dealId, Guid noteId) => Task.FromResult(false);
    }

    private sealed class FakePropertyRepository : IPropertyRepository
    {
        private readonly List<Property> _storage = [];

        public void AddInMemory(Property property) => _storage.Add(property);

        public Task<Property?> GetById(Guid id) => Task.FromResult(_storage.FirstOrDefault(x => x.Id == id));

        public Task<List<Property>> Get(int limit) => Task.FromResult(_storage.Take(limit).ToList());

        public Task<Property> Add(Property entity)
        {
            _storage.Add(entity);
            return Task.FromResult(entity);
        }

        public void Update(Property entity)
        {
        }

        public bool Delete(Property entity) => _storage.Remove(entity);

        public Task<List<Property>> GetAvailable(int limit, int offset) => Task.FromResult(new List<Property>());
        public Task<List<Property>> GetByResponsibleRealtor(Guid realtorId, DateTime fromUtc) => Task.FromResult(new List<Property>());
        public Task<List<Property>> GetByIds(IReadOnlyCollection<Guid> ids) => Task.FromResult(_storage.Where(x => ids.Contains(x.Id)).ToList());
        public Task<Property?> GetByIdForUpdateWithCriteria(Guid id) => GetById(id);
        public void AddCriterionValue(PropertyCriterionValue value)
        {
        }

        public Task<List<Application.DTOs.PropertyMatching.PropertyMatchCandidate>> GetCandidatesForRequirement(ClientRequirement requirement, int take)
            => Task.FromResult(new List<Application.DTOs.PropertyMatching.PropertyMatchCandidate>());
    }

    private sealed class FakeClientRepository : IClientRepository
    {
        private readonly List<Client> _storage = [];

        public void AddInMemory(Client client) => _storage.Add(client);

        public Task<Client?> GetByPhone(string phone) => Task.FromResult(_storage.FirstOrDefault(x => x.PhoneNumber == phone));

        public Task<Client?> GetByEmail(string email)
            => Task.FromResult(_storage.FirstOrDefault(x => string.Equals(x.Email, email, StringComparison.OrdinalIgnoreCase)));

        public Task<Client?> GetById(Guid id) => Task.FromResult(_storage.FirstOrDefault(x => x.Id == id));

        public Task<List<Client>> Get(int limit) => Task.FromResult(_storage.Take(limit).ToList());

        public Task<Client> Add(Client entity)
        {
            _storage.Add(entity);
            return Task.FromResult(entity);
        }

        public void Update(Client entity)
        {
        }

        public bool Delete(Client entity) => _storage.Remove(entity);
    }
}
