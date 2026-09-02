using Infrastructure.Identity;

namespace API.Auth
{
    public interface IJwtTokenGenerator
    {
        JwtTokenResult Generate(ApplicationUser user, IReadOnlyCollection<string> roles, Guid sessionId);
    }

    public record JwtTokenResult(string AccessToken, DateTime ExpiresAtUtc);
}
