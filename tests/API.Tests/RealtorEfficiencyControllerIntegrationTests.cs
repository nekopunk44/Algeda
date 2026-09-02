using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.DTOs.RealtorEfficiency;
using Application.Interfaces;
using Domain.Enums;
using Domain.Entities;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API.Tests;

public class RealtorEfficiencyControllerIntegrationTests :
    IClassFixture<RealtorEfficiencyControllerIntegrationTests.TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public RealtorEfficiencyControllerIntegrationTests(TestWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SubmitFeedback_ShouldReturn401_WhenUnauthenticated()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/realtor-efficiency/feedback",
            CreateFeedbackRequest(),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SubmitFeedback_ShouldReturn403_WhenRoleIsNotClient()
    {
        var client = _factory.CreateClient();
        AddAuthHeaders(client, AppRoles.Admin, "admin@example.com");

        var response = await client.PostAsJsonAsync(
            "/api/realtor-efficiency/feedback",
            CreateFeedbackRequest(),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SubmitFeedback_ShouldReturn200_ForClientRole()
    {
        var client = _factory.CreateClient();
        AddAuthHeaders(client, AppRoles.Client, "client@example.com");

        var response = await client.PostAsJsonAsync(
            "/api/realtor-efficiency/feedback",
            CreateFeedbackRequest(),
            JsonOptions);

        Assert.True(
            response.IsSuccessStatusCode,
            await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<RealtorFeedbackResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(FeedbackFormType.Purchase, body!.FormType);
    }

    [Fact]
    public async Task GetLatestScores_ShouldReturn403_WhenRoleIsClient()
    {
        var client = _factory.CreateClient();
        AddAuthHeaders(client, AppRoles.Client, "client@example.com");

        var response = await client.GetAsync($"/api/realtor-efficiency/realtors/{Guid.NewGuid()}/scores/latest");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetLatestScores_ShouldReturn200_ForAdmin()
    {
        var client = _factory.CreateClient();
        AddAuthHeaders(client, AppRoles.Admin, "admin@example.com");

        var response = await client.GetAsync($"/api/realtor-efficiency/realtors/{Guid.NewGuid()}/scores/latest");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<RealtorScoreBreakdownResponse>();
        Assert.NotNull(body);
        Assert.Equal(4.2, body!.ClientTrustScore);
    }

    [Fact]
    public async Task GetScoreHistory_ShouldReturn200_ForAdmin()
    {
        var client = _factory.CreateClient();
        AddAuthHeaders(client, AppRoles.Admin, "admin@example.com");

        var response = await client.GetAsync($"/api/realtor-efficiency/realtors/{Guid.NewGuid()}/scores/history?limit=5");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<List<RealtorScoreBreakdownResponse>>();
        Assert.NotNull(body);
        Assert.NotEmpty(body!);
    }

    private static SubmitDealFeedbackRequest CreateFeedbackRequest()
    {
        return new SubmitDealFeedbackRequest(
            DealId: Guid.NewGuid(),
            ServiceScore: 4,
            FormType: FeedbackFormType.Purchase,
            CommunicationScore: 5,
            ResponsivenessScore: 4,
            ExpertiseScore: 5,
            TitleAccuracyScore: 5,
            CriteriaAccuracyScore: 5,
            DescriptionAccuracyScore: 4,
            PhotosAccuracyScore: 5,
            Comment: "Все хорошо");
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
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName,
                    _ => { });

                services.RemoveAll<IRealtorFeedbackService>();
                services.RemoveAll<IRealtorEfficiencyCalculationService>();
                services.RemoveAll<ISystemSettingRepository>();

                services.AddSingleton<IRealtorFeedbackService, StubRealtorFeedbackService>();
                services.AddSingleton<IRealtorEfficiencyCalculationService, StubRealtorEfficiencyCalculationService>();
                services.AddSingleton<ISystemSettingRepository, MemorySystemSettingRepository>();
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

            var role = roleValues.ToString();
            var email = emailValues.ToString();
            var claims = new List<Claim>
            {
                new(ClaimTypes.Role, role),
                new(ClaimTypes.Email, email)
            };

            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private sealed class StubRealtorFeedbackService : IRealtorFeedbackService
    {
        public Task<RealtorFeedbackResponse> SubmitForCurrentClient(
            string clientEmail,
            SubmitDealFeedbackRequest request)
        {
            var response = new RealtorFeedbackResponse(
                Id: Guid.NewGuid(),
                DealId: request.DealId,
                ClientId: Guid.NewGuid(),
                ServiceRealtorId: Guid.NewGuid(),
                PropertyId: null,
                PropertyResponsibleRealtorId: null,
                ServiceScore: request.ServiceScore,
                FormType: request.FormType == FeedbackFormType.Undefined ? FeedbackFormType.Purchase : request.FormType,
                CommunicationScore: request.CommunicationScore,
                ResponsivenessScore: request.ResponsivenessScore,
                ExpertiseScore: request.ExpertiseScore,
                TitleAccuracyScore: request.TitleAccuracyScore,
                CriteriaAccuracyScore: request.CriteriaAccuracyScore,
                DescriptionAccuracyScore: request.DescriptionAccuracyScore,
                PhotosAccuracyScore: request.PhotosAccuracyScore,
                Comment: request.Comment,
                CreatedDate: DateTime.UtcNow);

            return Task.FromResult(response);
        }

        public Task<DealFeedbackStateResponse> GetStateForCurrentClient(string clientEmail, Guid dealId)
        {
            return Task.FromResult(new DealFeedbackStateResponse(
                DealId: dealId,
                FormType: FeedbackFormType.Purchase,
                IsCompleted: true,
                IsSubmitted: false,
                CanSubmit: true,
                CompletedAtUtc: DateTime.UtcNow.AddDays(-1),
                DeadlineUtc: DateTime.UtcNow.AddDays(13),
                DaysRemaining: 13,
                BlockReasonCode: null,
                BlockReason: null));
        }

        public Task<RealtorFeedbackSummaryResponse> GetSummaryForRealtor(Guid realtorId, int limit)
        {
            return Task.FromResult(new RealtorFeedbackSummaryResponse(
                RealtorId: realtorId,
                TotalFeedbackCount: 1,
                ServiceFeedbackCount: 1,
                PropertyFeedbackCount: 1,
                PurchaseFeedbackCount: 1,
                SaleFeedbackCount: 0,
                AverageServiceScore: 5,
                AverageCommunicationScore: 5,
                AverageResponsivenessScore: 5,
                AverageExpertiseScore: 5,
                AverageTitleAccuracyScore: 5,
                AverageDescriptionAccuracyScore: 5,
                AveragePhotosAccuracyScore: 5,
                AverageCriteriaAccuracyScore: 5,
                RecentItems: []));
        }
    }

    private sealed class StubRealtorEfficiencyCalculationService : IRealtorEfficiencyCalculationService
    {
        public Task<RealtorScoreBreakdownResponse> RecalculateAndSave(
            Guid realtorId,
            int? days = null,
            string? calculationVersion = null)
        {
            return Task.FromResult(BuildSnapshot(realtorId, calculationVersion));
        }

        public Task<RealtorScoreBreakdownResponse?> GetLatestBreakdown(Guid realtorId)
        {
            return Task.FromResult<RealtorScoreBreakdownResponse?>(BuildSnapshot(realtorId, "stub-latest"));
        }

        public Task<List<RealtorScoreBreakdownResponse>> GetHistory(Guid realtorId, int limit = 20)
        {
            var result = Enumerable.Range(1, Math.Clamp(limit, 1, 5))
                .Select(_ => BuildSnapshot(realtorId, "stub-history"))
                .ToList();

            return Task.FromResult(result);
        }

        private static RealtorScoreBreakdownResponse BuildSnapshot(Guid realtorId, string? version)
        {
            return new RealtorScoreBreakdownResponse(
                SnapshotId: Guid.NewGuid(),
                RealtorId: realtorId,
                ClientTrustScore: 4.2,
                AdminPerformanceScore: 3.8,
                ClientTrustBreakdown: new ClientTrustBreakdownResponse(4.3, 0, 4.0),
                AdminPerformanceBreakdown: new AdminPerformanceBreakdownResponse(3.9, 3.7, 3.8, 3.6),
                CalculationVersion: version,
                CreatedDate: DateTime.UtcNow);
        }
    }

    private sealed class MemorySystemSettingRepository : ISystemSettingRepository
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public string? GetValue(string key) =>
            _values.TryGetValue(key, out var value) ? value : null;

        public void Upsert(string key, string value) => _values[key] = value;

        public Task<SystemSetting?> GetById(Guid id) => Task.FromResult<SystemSetting?>(null);

        public Task<List<SystemSetting>> Get(int limit) => Task.FromResult(new List<SystemSetting>());

        public Task<SystemSetting> Add(SystemSetting entity) => Task.FromResult(entity);

        public void Update(SystemSetting entity)
        {
        }

        public bool Delete(SystemSetting entity) => true;
    }
}
