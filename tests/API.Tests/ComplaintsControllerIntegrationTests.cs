using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.DTOs.PropertyMatching;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Primitives;
using Domain.ValueObjects;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API.Tests;

public class ComplaintsControllerIntegrationTests :
    IClassFixture<ComplaintsControllerIntegrationTests.TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ComplaintsControllerIntegrationTests(TestWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_ShouldReturn401_WhenUnauthenticated()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/complaints", new
        {
            category = "Other",
            subject = "Тема",
            description = "Описание"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_ShouldReturn403_WhenRoleIsNotClient()
    {
        var client = _factory.CreateClient();
        AddAuthHeaders(client, AppRoles.Admin, "admin@example.com");

        var response = await client.PostAsJsonAsync("/api/complaints", new
        {
            category = "Other",
            subject = "Тема",
            description = "Описание"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_ShouldIgnoreBodyClientId_AndUseAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        AddAuthHeaders(client, AppRoles.Client, "client1@example.com");
        var spoofedClientId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync("/api/complaints", new
        {
            clientId = spoofedClientId,
            category = "Other",
            subject = "Общая жалоба",
            description = "Плохой результат подбора."
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ComplaintResponseDto>(JsonOptions);
        Assert.NotNull(body);
        Assert.NotEqual(spoofedClientId, body!.ClientId);
    }

    [Fact]
    public async Task Create_ShouldReturn404_WhenDealIsNotOwnedByClient()
    {
        var client = _factory.CreateClient();
        AddAuthHeaders(client, AppRoles.Client, "client1@example.com");

        var response = await client.PostAsJsonAsync("/api/complaints", new
        {
            category = "Other",
            dealId = TestStore.Client2DealId,
            subject = "Жалоба по чужой сделке",
            description = "Должна быть запрещена."
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AdminGet_ShouldFilterByCategoryAndDealLink()
    {
        var client = _factory.CreateClient();

        AddAuthHeaders(client, AppRoles.Client, "client1@example.com");
        var createLinked = await client.PostAsJsonAsync("/api/complaints", new
        {
            category = 3,
            dealId = TestStore.Client1DealId,
            subject = "Неверное описание",
            description = "Описание не совпадает."
        });
        createLinked.EnsureSuccessStatusCode();

        var createGeneral = await client.PostAsJsonAsync("/api/complaints", new
        {
            category = 4,
            subject = "Общая жалоба",
            description = "Общий комментарий."
        });
        createGeneral.EnsureSuccessStatusCode();

        var createLinkedOtherCategory = await client.PostAsJsonAsync("/api/complaints", new
        {
            category = 4,
            dealId = TestStore.Client1DealId,
            subject = "Еще одна жалоба по сделке",
            description = "С другой категорией."
        });
        createLinkedOtherCategory.EnsureSuccessStatusCode();

        AddAuthHeaders(client, AppRoles.Admin, "admin@example.com");
        var response = await client.GetAsync("/api/complaints?category=3&dealLinked=true");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<List<ComplaintResponseDto>>(JsonOptions);
        Assert.NotNull(body);
        Assert.NotEmpty(body!);
    }

    private static void AddAuthHeaders(HttpClient client, string role, string email)
    {
        client.DefaultRequestHeaders.Remove(TestAuthHandler.RoleHeader);
        client.DefaultRequestHeaders.Remove(TestAuthHandler.EmailHeader);
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, role);
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, email);
    }

    public sealed class TestWebAppFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName,
                    _ => { });

                services.RemoveAll<IClientRepository>();
                services.RemoveAll<IRealtorRepository>();
                services.RemoveAll<IDealRepository>();
                services.RemoveAll<IPropertyRepository>();
                services.RemoveAll<IComplaintRepository>();

                services.AddSingleton(TestStore.Create());
                services.AddSingleton<IClientRepository, StubClientRepository>();
                services.AddSingleton<IRealtorRepository, StubRealtorRepository>();
                services.AddSingleton<IDealRepository, StubDealRepository>();
                services.AddSingleton<IPropertyRepository, StubPropertyRepository>();
                services.AddSingleton<IComplaintRepository, StubComplaintRepository>();
            });
        }
    }

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Test";
        public const string RoleHeader = "X-Test-Role";
        public const string EmailHeader = "X-Test-Email";

        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(RoleHeader, out var roleValues)
                || !Request.Headers.TryGetValue(EmailHeader, out var emailValues))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.Role, roleValues.ToString()),
                new(ClaimTypes.Email, emailValues.ToString())
            };

            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private sealed class StubClientRepository : IClientRepository
    {
        private readonly TestStore _store;

        public StubClientRepository(TestStore store)
        {
            _store = store;
        }

        public Task<Client?> GetByPhone(string phone)
        {
            var client = _store.Clients.Values.FirstOrDefault(x => x.PhoneNumber == phone);
            return Task.FromResult(client);
        }

        public Task<Client?> GetByEmail(string email)
        {
            var normalized = email.Trim().ToLowerInvariant();
            var client = _store.Clients.Values.FirstOrDefault(x => string.Equals(x.Email, normalized, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(client);
        }

        public Task<Client?> GetById(Guid id)
        {
            _store.Clients.TryGetValue(id, out var client);
            return Task.FromResult(client);
        }

        public Task<List<Client>> Get(int limit)
        {
            return Task.FromResult(_store.Clients.Values.Take(limit).ToList());
        }

        public Task<Client> Add(Client entity)
        {
            _store.Clients[entity.Id] = entity;
            return Task.FromResult(entity);
        }

        public void Update(Client entity)
        {
            _store.Clients[entity.Id] = entity;
        }

        public bool Delete(Client entity)
        {
            return _store.Clients.Remove(entity.Id);
        }
    }

    private sealed class StubRealtorRepository : IRealtorRepository
    {
        private readonly TestStore _store;

        public StubRealtorRepository(TestStore store)
        {
            _store = store;
        }

        public Task<Realtor?> GetByPhone(string phone)
        {
            var realtor = _store.Realtors.Values.FirstOrDefault(x => x.PhoneNumber == phone);
            return Task.FromResult(realtor);
        }

        public Task<List<Realtor>> GetTopRealtors(int count)
        {
            return Task.FromResult(_store.Realtors.Values.Take(count).ToList());
        }

        public Task<List<Realtor>> GetActive()
        {
            return Task.FromResult(_store.Realtors.Values.ToList());
        }

        public Task<Realtor?> GetById(Guid id)
        {
            _store.Realtors.TryGetValue(id, out var realtor);
            return Task.FromResult(realtor);
        }

        public Task<List<Realtor>> Get(int limit)
        {
            return Task.FromResult(_store.Realtors.Values.Take(limit).ToList());
        }

        public Task<Realtor> Add(Realtor entity)
        {
            _store.Realtors[entity.Id] = entity;
            return Task.FromResult(entity);
        }

        public void Update(Realtor entity)
        {
            _store.Realtors[entity.Id] = entity;
        }

        public bool Delete(Realtor entity)
        {
            return _store.Realtors.Remove(entity.Id);
        }
    }

    private sealed class StubDealRepository : IDealRepository
    {
        private readonly TestStore _store;

        public StubDealRepository(TestStore store)
        {
            _store = store;
        }

        public Task<TResult> ExecuteWorkflow<TResult>(Guid dealId, Func<Task<TResult>> operation) => operation();

        public Task<Deal?> GetById(Guid id)
        {
            _store.Deals.TryGetValue(id, out var deal);
            return Task.FromResult(deal);
        }

        public Task<List<Deal>> Get(int limit) => Task.FromResult(_store.Deals.Values.Take(limit).ToList());
        public Task<Deal> Add(Deal entity)
        {
            _store.Deals[entity.Id] = entity;
            return Task.FromResult(entity);
        }

        public void Update(Deal entity)
        {
            _store.Deals[entity.Id] = entity;
        }

        public bool Delete(Deal entity)
        {
            return _store.Deals.Remove(entity.Id);
        }
        public Task<Deal?> GetByIdWithNotes(Guid id) => GetById(id);
        public Task<List<Deal>> GetIncoming(int limit) => Task.FromResult(new List<Deal>());
        public Task<List<Deal>> GetIncomingOrAssignedToRealtor(Guid realtorId, int limit) => Task.FromResult(new List<Deal>());
        public Task<List<Deal>> GetByRealtor(Guid realtorId) => Task.FromResult(_store.Deals.Values.Where(x => x.RealtorId == realtorId).ToList());
        public Task<List<Deal>> GetByClient(Guid clientId) => Task.FromResult(_store.Deals.Values.Where(x => x.ClientId == clientId).ToList());
        public Task<List<Deal>> GetByProperty(Guid propertyId) => Task.FromResult(_store.Deals.Values.Where(x => x.PropertyId == propertyId).ToList());
        public Task<Deal?> GetLatestSaleRequestByProperty(Guid propertyId) => Task.FromResult(_store.Deals.Values.FirstOrDefault(x => x.PropertyId == propertyId && x.Source == DealSource.Sale));
        public Task<List<Deal>> GetCompletedByRealtor(Guid realtorId, DateTime fromUtc) => Task.FromResult(_store.Deals.Values.Where(x => x.RealtorId == realtorId && x.Status == DealStatus.Completed && x.CompletedAt.HasValue && x.CompletedAt.Value >= fromUtc).ToList());
        public Task<DealNote?> AddNote(Guid dealId, string text, Guid? authorRealtorId) => Task.FromResult<DealNote?>(null);
        public Task<DealNote?> UpdateNote(Guid dealId, Guid noteId, string text) => Task.FromResult<DealNote?>(null);
        public Task<bool> DeleteNote(Guid dealId, Guid noteId) => Task.FromResult(false);
    }

    private sealed class StubPropertyRepository : IPropertyRepository
    {
        private readonly TestStore _store;

        public StubPropertyRepository(TestStore store)
        {
            _store = store;
        }

        public Task<Property?> GetById(Guid id)
        {
            _store.Properties.TryGetValue(id, out var property);
            return Task.FromResult(property);
        }

        public Task<List<Property>> Get(int limit) => Task.FromResult(_store.Properties.Values.Take(limit).ToList());
        public Task<Property> Add(Property entity)
        {
            _store.Properties[entity.Id] = entity;
            return Task.FromResult(entity);
        }

        public void Update(Property entity)
        {
            _store.Properties[entity.Id] = entity;
        }

        public bool Delete(Property entity)
        {
            return _store.Properties.Remove(entity.Id);
        }

        public Task<List<Property>> GetAvailable(int limit, int offset) => Task.FromResult(_store.Properties.Values.Skip(offset).Take(limit).ToList());
        public Task<List<Property>> GetByResponsibleRealtor(Guid realtorId, DateTime fromUtc) => Task.FromResult(_store.Properties.Values.Where(x => x.ResponsibleRealtorId == realtorId).ToList());
        public Task<List<Property>> GetByIds(IReadOnlyCollection<Guid> ids) => Task.FromResult(_store.Properties.Values.Where(x => ids.Contains(x.Id)).ToList());
        public Task<Property?> GetByIdForUpdateWithCriteria(Guid id) => GetById(id);
        public void AddCriterionValue(PropertyCriterionValue value)
        {
        }

        public Task<List<PropertyMatchCandidate>> GetCandidatesForRequirement(ClientRequirement requirement, int take)
        {
            var items = _store.Properties.Values
                .Take(take)
                .Select(x => new PropertyMatchCandidate(x, 0))
                .ToList();

            return Task.FromResult(items);
        }
    }

    private sealed class StubComplaintRepository : IComplaintRepository
    {
        private readonly TestStore _store;

        public StubComplaintRepository(TestStore store)
        {
            _store = store;
        }

        public Task<Complaint?> GetById(Guid id)
        {
            _store.Complaints.TryGetValue(id, out var complaint);
            return Task.FromResult(complaint);
        }

        public Task<List<Complaint>> Get(int limit)
        {
            return Task.FromResult(_store.Complaints.Values.Take(limit).ToList());
        }

        public Task<Complaint> Add(Complaint entity)
        {
            _store.Complaints[entity.Id] = entity;
            return Task.FromResult(entity);
        }

        public void Update(Complaint entity)
        {
            _store.Complaints[entity.Id] = entity;
        }

        public bool Delete(Complaint entity)
        {
            return _store.Complaints.Remove(entity.Id);
        }

        public Task<List<Complaint>> GetOpenComplaints()
        {
            return Task.FromResult(_store.Complaints.Values.Where(x => x.Status != ComplaintStatus.Resolved).ToList());
        }

        public Task<List<Complaint>> GetResolvedByRealtor(Guid realtorId, DateTime fromUtc)
        {
            return Task.FromResult(_store.Complaints.Values
                .Where(x => x.TargetRealtorId == realtorId && x.Status == ComplaintStatus.Resolved && x.ResolvedAt.HasValue && x.ResolvedAt.Value >= fromUtc)
                .ToList());
        }

        public Task<int> CountByClientAndSubjectBase(Guid clientId, string subjectBase)
        {
            if (string.IsNullOrWhiteSpace(subjectBase))
            {
                return Task.FromResult(0);
            }

            var normalizedBase = subjectBase.Trim();
            var prefix = $"{normalizedBase} #";

            var count = _store.Complaints.Values.Count(x =>
                x.ClientId == clientId
                && (x.Subject == normalizedBase || x.Subject.StartsWith(prefix, StringComparison.Ordinal)));

            return Task.FromResult(count);
        }

        public Task<List<Complaint>> GetForAdmin(int limit, ComplaintStatus? status = null, ComplaintCategory? category = null, bool? dealLinked = null)
        {
            IEnumerable<Complaint> query = _store.Complaints.Values;

            if (status.HasValue)
                query = query.Where(x => x.Status == status.Value);

            if (category.HasValue)
                query = query.Where(x => x.Category == category.Value);

            if (dealLinked.HasValue)
                query = dealLinked.Value ? query.Where(x => x.DealId.HasValue) : query.Where(x => !x.DealId.HasValue);

            return Task.FromResult(query.Take(limit).ToList());
        }

        public Task<List<Complaint>> GetForRealtor(Guid realtorId, int limit)
        {
            var query = _store.Complaints.Values
                .Where(x => x.TargetRealtorId == realtorId)
                .OrderByDescending(x => x.CreatedDate)
                .Take(limit)
                .ToList();

            return Task.FromResult(query);
        }
    }

    private sealed class TestStore
    {
        public static readonly Guid Client1DealId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        public static readonly Guid Client2DealId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        public Dictionary<Guid, Client> Clients { get; } = [];
        public Dictionary<Guid, Realtor> Realtors { get; } = [];
        public Dictionary<Guid, Property> Properties { get; } = [];
        public Dictionary<Guid, Deal> Deals { get; } = [];
        public Dictionary<Guid, Complaint> Complaints { get; } = [];

        public static TestStore Create()
        {
            var store = new TestStore();

            var client1 = new Client(new FullName("Client", "One", null), "+37360000001", "client1@example.com");
            var client2 = new Client(new FullName("Client", "Two", null), "+37360000002", "client2@example.com");
            var realtor = new Realtor(new FullName("Realtor", "Main", null), "+37361111111");

            SetId(client1, Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
            SetId(client2, Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
            SetId(realtor, Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"));

            var property1 = CreateProperty(Guid.Parse("10000000-0000-0000-0000-000000000001"), realtor.Id);
            var property2 = CreateProperty(Guid.Parse("20000000-0000-0000-0000-000000000002"), realtor.Id);

            var deal1 = new Deal(
                propertyId: Guid.Parse("10000000-0000-0000-0000-000000000001"),
                clientId: client1.Id,
                realtorId: realtor.Id,
                source: DealSource.Manual);
            var deal2 = new Deal(
                propertyId: Guid.Parse("20000000-0000-0000-0000-000000000002"),
                clientId: client2.Id,
                realtorId: realtor.Id,
                source: DealSource.Manual);

            SetId(deal1, Client1DealId);
            SetId(deal2, Client2DealId);

            store.Clients[client1.Id] = client1;
            store.Clients[client2.Id] = client2;
            store.Realtors[realtor.Id] = realtor;
            store.Properties[property1.Id] = property1;
            store.Properties[property2.Id] = property2;
            store.Deals[deal1.Id] = deal1;
            store.Deals[deal2.Id] = deal2;

            return store;
        }

        private static Property CreateProperty(Guid id, Guid realtorId)
        {
            var property = new Property(
                title: "Test property",
                address: "Test address",
                price: 100000m,
                area: 50,
                roomsCount: 2,
                location: new Location(47.0105, 28.8638),
                type: PropertyType.Apartment,
                responsibleRealtorId: realtorId);

            SetId(property, id);
            return property;
        }

        private static void SetId(object entity, Guid id)
        {
            var idProperty = entity.GetType().GetProperty("Id");
            idProperty!.SetValue(entity, id);
        }
    }

    private sealed class ComplaintResponseDto
    {
        public Guid Id { get; set; }
        public Guid ClientId { get; set; }
        public Guid? TargetRealtorId { get; set; }
        public Guid? DealId { get; set; }
        public ComplaintCategory Category { get; set; }
        public ComplaintStatus Status { get; set; }
    }
}
