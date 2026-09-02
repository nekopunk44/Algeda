using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Application.DTOs.Deal;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Enums;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API.Tests;

public class ClientDealsControllerIntegrationTests :
    IClassFixture<ClientDealsControllerIntegrationTests.TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public ClientDealsControllerIntegrationTests(TestWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetMine_ShouldReturn401_WhenUnauthenticated()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/client-deals/mine");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(AppRoles.Realtor)]
    [InlineData(AppRoles.Admin)]
    public async Task GetMine_ShouldReturn403_WhenRoleIsNotClient(string role)
    {
        var client = _factory.CreateClient();
        AddAuthHeaders(client, role, "actor@example.com");

        var response = await client.GetAsync("/api/client-deals/mine");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetMine_ShouldReturnOnlyCurrentClientDeals_WhenClient()
    {
        var client = _factory.CreateClient();
        AddAuthHeaders(client, AppRoles.Client, "client1@example.com");

        var response = await client.GetAsync("/api/client-deals/mine?scope=purchase");

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, json.ValueKind);
        Assert.Equal(1, json.GetArrayLength());

        var first = json.EnumerateArray().First();
        Assert.Equal("10000000-0000-0000-0000-000000000001", first.GetProperty("clientId").GetString());
    }

    [Fact]
    public async Task GetMineById_ShouldReturn404_WhenDealIsNotOwnedByClient()
    {
        var client = _factory.CreateClient();
        AddAuthHeaders(client, AppRoles.Client, "client1@example.com");

        var response = await client.GetAsync($"/api/client-deals/mine/{Guid.Parse("00000000-0000-0000-0000-000000000099")}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(AppRoles.Realtor, HttpStatusCode.Forbidden)]
    public async Task LegacySaleRequestsEndpoint_ShouldKeepAuthorizationBehavior(string? role, HttpStatusCode expected)
    {
        var client = _factory.CreateClient();

        if (!string.IsNullOrWhiteSpace(role))
        {
            AddAuthHeaders(client, role, "legacy@example.com");
        }

        var response = await client.GetAsync("/api/deals/me/sale-requests");

        Assert.Equal(expected, response.StatusCode);
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

                services.RemoveAll<IClientDealCenterService>();
                services.AddSingleton<IClientDealCenterService, StubClientDealCenterService>();
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

    private sealed class StubClientDealCenterService : IClientDealCenterService
    {
        private static readonly Guid ClientOneId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        private static readonly Guid ClientTwoId = Guid.Parse("20000000-0000-0000-0000-000000000002");
        private static readonly Guid ClientOneDealId = Guid.Parse("00000000-0000-0000-0000-000000000011");
        private static readonly Guid ClientTwoDealId = Guid.Parse("00000000-0000-0000-0000-000000000022");

        public Task<List<DealWorkflowResponse>> GetMyDeals(
            string email,
            int limit,
            string? search,
            DealStatus? status,
            string? scope)
        {
            var deals = BuildDealsFor(email)
                .Take(Math.Clamp(limit, 1, 500))
                .ToList();

            return Task.FromResult(deals);
        }

        public Task<DealWorkflowResponse> GetMyDealById(string email, Guid dealId)
        {
            var deal = BuildDealsFor(email).FirstOrDefault(x => x.Id == dealId);
            if (deal is null)
            {
                throw new NotFoundException("Сделка не найдена.");
            }

            return Task.FromResult(deal);
        }

        private static IReadOnlyList<DealWorkflowResponse> BuildDealsFor(string email)
        {
            if (string.Equals(email, "client1@example.com", StringComparison.OrdinalIgnoreCase))
            {
                return [
                    BuildDeal(ClientOneDealId, ClientOneId, DealSource.Matching)
                ];
            }

            if (string.Equals(email, "client2@example.com", StringComparison.OrdinalIgnoreCase))
            {
                return [
                    BuildDeal(ClientTwoDealId, ClientTwoId, DealSource.Sale)
                ];
            }

            return [];
        }

        private static DealWorkflowResponse BuildDeal(Guid dealId, Guid clientId, DealSource source)
        {
            return new DealWorkflowResponse(
                Id: dealId,
                ClientId: clientId,
                ClientFullName: "Client Test",
                ClientPhoneNumber: "+37300000000",
                ClientEmail: "client@example.com",
                PropertyId: Guid.Parse("30000000-0000-0000-0000-000000000003"),
                PropertyTitle: "Property",
                ClientRequirementId: null,
                Source: source,
                Status: DealStatus.InProgress,
                IsIncoming: false,
                RealtorId: Guid.Parse("40000000-0000-0000-0000-000000000004"),
                RealtorFullName: "Realtor Test",
                RealtorPhoneNumber: "+37311111111",
                RealtorEmail: "realtor@example.com",
                RequestMessage: "Request",
                AcceptedAtUtc: DateTime.UtcNow.AddDays(-1),
                RejectedAtUtc: null,
                PriorityRealtorId: null,
                PriorityUntilUtc: null,
                CompletedAtUtc: DateTime.UtcNow.AddHours(-6),
                Notes: [],
                CreatedDate: DateTime.UtcNow.AddDays(-2));
        }
    }
}
