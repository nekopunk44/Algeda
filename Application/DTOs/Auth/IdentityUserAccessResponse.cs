namespace Application.DTOs.Auth
{
    public record IdentityUserAccessResponse(
        Guid UserId,
        string Email,
        string? DisplayName,
        bool EmailConfirmed,
        bool IsFrozen,
        List<string> Roles);

    public record TransferSuperAdminRequest(
        Guid TargetUserId,
        string Password,
        bool Confirmed);
}
