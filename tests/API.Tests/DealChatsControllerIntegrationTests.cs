using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Application.DTOs.DealChat;
using Application.Exceptions;
using Application.Interfaces;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API.Tests;

public class DealChatsControllerIntegrationTests :
    IClassFixture<DealChatsControllerIntegrationTests.TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public DealChatsControllerIntegrationTests(TestWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetDialog_ShouldReturn401_WhenUnauthenticated()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/deal-chats/{TestStore.DealOneId}/messages");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDialog_ShouldReturn200_ForDealParticipantClient()
    {
        var client = _factory.CreateClient();
        AddAuthHeaders(client, AppRoles.Client, "client1@example.com");

        var response = await client.GetAsync($"/api/deal-chats/{TestStore.DealOneId}/messages");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetDialog_ShouldReturn404_ForForeignClient()
    {
        var client = _factory.CreateClient();
        AddAuthHeaders(client, AppRoles.Client, "client2@example.com");

        var response = await client.GetAsync($"/api/deal-chats/{TestStore.DealOneId}/messages");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Send_ShouldReturn200_ForAssignedRealtor()
    {
        var client = _factory.CreateClient();
        AddAuthHeaders(client, AppRoles.Realtor, "realtor1@example.com");

        var response = await client.PostAsJsonAsync($"/api/deal-chats/{TestStore.DealOneId}/messages", new
        {
            content = "Принял, работаем"
        });

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task AdminChats_ShouldReturn403_ForNonAdmin()
    {
        var client = _factory.CreateClient();
        AddAuthHeaders(client, AppRoles.Client, "client1@example.com");

        var response = await client.GetAsync("/api/deal-chats/admin");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminChats_ShouldReturn200_ForAdmin()
    {
        var client = _factory.CreateClient();
        AddAuthHeaders(client, AppRoles.Admin, "admin@example.com");

        var response = await client.GetAsync("/api/deal-chats/admin");

        response.EnsureSuccessStatusCode();
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

                services.RemoveAll<IDealChatService>();
                services.AddSingleton<IDealChatService, StubDealChatService>();
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

    private static class TestStore
    {
        public static readonly Guid DealOneId = Guid.Parse("00000000-0000-0000-0000-000000000111");
        public static readonly Guid ClientOneId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        public static readonly Guid ClientTwoId = Guid.Parse("20000000-0000-0000-0000-000000000002");
        public static readonly Guid RealtorOneId = Guid.Parse("30000000-0000-0000-0000-000000000003");
    }

    private sealed class StubDealChatService : IDealChatService
    {
        public Task<bool> CanAccessDealChat(Guid dealId, string email, bool isAdmin, bool isClient, bool isRealtor)
        {
            if (isAdmin)
                return Task.FromResult(true);

            if (dealId != TestStore.DealOneId)
                return Task.FromResult(false);

            if (isClient && string.Equals(email, "client1@example.com", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(true);

            if (isRealtor && string.Equals(email, "realtor1@example.com", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(true);

            return Task.FromResult(false);
        }

        public async Task<DealChatDialogResponse> GetDialogForCurrentUser(Guid dealId, string email, bool isAdmin, bool isClient, bool isRealtor, int limit)
        {
            if (!await CanAccessDealChat(dealId, email, isAdmin, isClient, isRealtor))
                throw new NotFoundException("Чат по сделке не найден.");

            return new DealChatDialogResponse(
                dealId,
                TestStore.ClientOneId,
                TestStore.RealtorOneId,
                isClient ? TestStore.ClientOneId : TestStore.RealtorOneId,
                "Client One",
                "Realtor One",
                !isAdmin,
                isAdmin ? "Администратор может только просматривать чат." : null,
                []);
        }

        public async Task<DealChatMessageResponse> SendMessageForCurrentUser(Guid dealId, string email, bool isAdmin, bool isClient, bool isRealtor, string content)
        {
            if (isAdmin)
                throw new ValidationException("Администратор может только просматривать чат.");

            if (!await CanAccessDealChat(dealId, email, isAdmin, isClient, isRealtor))
                throw new NotFoundException("Чат по сделке не найден.");

            var senderId = isClient ? TestStore.ClientOneId : TestStore.RealtorOneId;
            var receiverId = isClient ? TestStore.RealtorOneId : TestStore.ClientOneId;

            return new DealChatMessageResponse(
                Guid.NewGuid(),
                dealId,
                senderId,
                receiverId,
                content,
                false,
                DateTime.UtcNow,
                true);
        }

        public Task<IReadOnlyList<DealChatSummaryResponse>> GetAdminChats(Guid? dealId, Guid? clientId, Guid? realtorId, int limit)
        {
            var item = new DealChatSummaryResponse(
                TestStore.DealOneId,
                TestStore.ClientOneId,
                "Client One",
                TestStore.RealtorOneId,
                "Realtor One",
                "InProgress",
                DateTime.UtcNow,
                "Последнее сообщение",
                3);

            return Task.FromResult<IReadOnlyList<DealChatSummaryResponse>>([item]);
        }

        public Task<IReadOnlyList<DealChatNotificationResponse>> GetUnreadNotificationsForCurrentUser(
            string email,
            bool isAdmin,
            bool isClient,
            bool isRealtor,
            int limit)
        {
            return Task.FromResult<IReadOnlyList<DealChatNotificationResponse>>([]);
        }

        public Task<int> MarkAllNotificationsAsReadForCurrentUser(
            string email,
            bool isAdmin,
            bool isClient,
            bool isRealtor)
        {
            return Task.FromResult(0);
        }
    }
}
