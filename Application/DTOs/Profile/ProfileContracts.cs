namespace Application.DTOs.Profile;

public sealed record UserProfileResponse(
    string Email,
    string FirstName,
    string LastName,
    string? MiddleName,
    string PhoneNumber,
    bool IsRealtor,
    string? AvatarPath,
    string? RealtorLevel = null,
    bool? IsLevelManuallyAssigned = null,
    decimal? RealtorCommissionPercent = null,
    decimal? RealtorPayoutThisMonth = null,
    decimal? RealtorPayoutTotal = null,
    string RealtorPayoutCurrency = "USD",
    bool IsClient = false,
    bool IsAdmin = false);

public sealed record UpdateUserProfileRequest(
    string Email,
    string FirstName,
    string LastName,
    string? MiddleName,
    string? PhoneNumber);

public sealed record RequestEmailChangeRequest(string NewEmail);

public sealed record ConfirmEmailChangeRequest(string NewEmail, string Code);

public sealed record ChangeMyPasswordRequest(
    string CurrentPassword,
    string NewPassword,
    string ConfirmPassword);

public sealed record UserSessionResponse(
    Guid Id,
    string DeviceName,
    string? IpAddress,
    DateTime CreatedAtUtc,
    DateTime LastSeenAtUtc,
    DateTime ExpiresAtUtc,
    bool IsCurrent);
