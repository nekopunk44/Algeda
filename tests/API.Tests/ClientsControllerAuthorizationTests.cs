using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Application.Interfaces;
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

public class ClientsControllerAuthorizationTests :
    IClassFixture<ClientsControllerAuthorizationTests.TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public ClientsControllerAuthorizationTests(TestWebAppFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(AppRoles.Client, HttpStatusCode.Forbidden)]
    [InlineData(AppRoles.Realtor, HttpStatusCode.OK)]
    [InlineData(AppRoles.Admin, HttpStatusCode.OK)]
    [InlineData(AppRoles.SuperAdmin, HttpStatusCode.OK)]
    public async Task Get_ShouldEnforceRealtorOrAdminRoleMatrix(
        string? role,
        HttpStatusCode expectedStatusCode)
    {
        var client = _factory.CreateClient();

        if (!string.IsNullOrWhiteSpace(role))
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, role);
        }

        var response = await client.GetAsync("/api/clients");

        Assert.Equal(expectedStatusCode, response.StatusCode);
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

                services.RemoveAll<IClientRepository>();
                services.AddSingleton<IClientRepository, EmptyClientRepository>();
            });
        }
    }

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Test";
        public const string RoleHeader = "X-Test-Role";

        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(RoleHeader, out var roleValues))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.Role, roleValues.ToString())
            };

            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private sealed class EmptyClientRepository : IClientRepository
    {
        public Task<Client?> GetByPhone(string phone) => Task.FromResult<Client?>(null);

        public Task<Client?> GetByEmail(string email) => Task.FromResult<Client?>(null);

        public Task<Client?> GetById(Guid id) => Task.FromResult<Client?>(null);

        public Task<List<Client>> Get(int limit) => Task.FromResult(new List<Client>());

        public Task<Client> Add(Client entity) => Task.FromResult(entity);

        public void Update(Client entity)
        {
        }

        public bool Delete(Client entity) => false;
    }
}
