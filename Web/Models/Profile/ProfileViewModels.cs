using System.ComponentModel.DataAnnotations;
using Web.Models.Api;

namespace Web.Models.Profile;

public sealed class ProfilePageViewModel
{
    public string? SuccessMessage { get; init; }
    public ApiErrorViewModel? ApiError { get; init; }
    public ProfileDetailsViewModel? Profile { get; init; }
    public ProfileEditFormViewModel EditForm { get; init; } = new();
    public ConfirmEmailChangeFormViewModel EmailChangeForm { get; init; } = new();
    public bool ShowEmailChangeConfirmation { get; init; }
    public ChangePasswordFormViewModel PasswordForm { get; init; } = new();
    public IReadOnlyList<ProfileComplaintViewModel> RealtorComplaints { get; init; } = [];
    public IReadOnlyList<AccountSessionViewModel> Sessions { get; init; } = [];
    public string DefaultAvatarPath { get; init; } = "/images/realtor-default-avatar.svg";
}

public sealed class ConfirmEmailChangeFormViewModel
{
    [Required(ErrorMessage = "Введите новый email.")]
    [EmailAddress(ErrorMessage = "Введите корректный email.")]
    [StringLength(256)]
    public string NewEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Введите код подтверждения.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Код должен состоять из 6 цифр.")]
    public string Code { get; set; } = string.Empty;
}

public sealed class ProfileDetailsViewModel
{
    public string Email { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? MiddleName { get; init; }
    public string PhoneNumber { get; init; } = string.Empty;
    public bool IsRealtor { get; init; }
    public bool IsClient { get; init; }
    public bool IsAdmin { get; init; }
    public bool HasContactProfile => IsRealtor || IsClient;
    public string? AvatarPath { get; init; }
    public string? RealtorLevel { get; init; }
    public bool? IsLevelManuallyAssigned { get; init; }
    public decimal? RealtorCommissionPercent { get; init; }
    public decimal? RealtorPayoutThisMonth { get; init; }
    public decimal? RealtorPayoutTotal { get; init; }
    public string RealtorPayoutCurrency { get; init; } = "USD";
}

public sealed class ProfileEditFormViewModel
{
    [Required(ErrorMessage = "Введите имя.")]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Введите фамилию.")]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [StringLength(100)]
    public string? MiddleName { get; set; }

    [Required(ErrorMessage = "Введите email.")]
    [EmailAddress(ErrorMessage = "Введите корректный email.")]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [StringLength(32)]
    public string? PhoneNumber { get; set; }
}

public sealed class ChangePasswordFormViewModel
{
    [Required(ErrorMessage = "Введите текущий пароль.")]
    [StringLength(128)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Введите новый пароль.")]
    [MinLength(8, ErrorMessage = "Новый пароль должен содержать минимум 8 символов.")]
    [StringLength(128)]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Подтвердите новый пароль.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Пароль и подтверждение пароля не совпадают.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class UpdateProfileRequest
{
    public string Email { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? MiddleName { get; init; }
    public string? PhoneNumber { get; init; }
}

public sealed class ChangePasswordRequest
{
    public string CurrentPassword { get; init; } = string.Empty;
    public string NewPassword { get; init; } = string.Empty;
    public string ConfirmPassword { get; init; } = string.Empty;
}

public sealed class RequestEmailChangeRequest
{
    public string NewEmail { get; init; } = string.Empty;
}

public sealed class ConfirmEmailChangeRequest
{
    public string NewEmail { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
}

public sealed class AccountSessionViewModel
{
    public Guid Id { get; init; }
    public string DeviceName { get; init; } = string.Empty;
    public string? IpAddress { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime LastSeenAtUtc { get; init; }
    public DateTime ExpiresAtUtc { get; init; }
    public bool IsCurrent { get; init; }
}

public sealed class ProfileComplaintViewModel
{
    public Guid Id { get; init; }
    public string Category { get; init; } = "Undefined";
    public string Subject { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Status { get; init; } = "Undefined";
    public string ModerationVerdict { get; init; } = "Undefined";
    public Guid? DealId { get; init; }
    public Guid? PropertyId { get; init; }
    public string? AdminResolution { get; init; }
    public DateTime CreatedDate { get; init; }
    public DateTime? ResolvedAt { get; init; }
}
