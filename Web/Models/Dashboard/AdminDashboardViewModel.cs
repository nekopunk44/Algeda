using Web.Models.Api;

namespace Web.Models.Dashboard;

public class AdminPageBaseViewModel
{
    public string? SuccessMessage { get; init; }

    public ApiErrorViewModel? ApiError { get; init; }
}

public sealed class AdminCriteriaViewModel : AdminPageBaseViewModel
{
    public DashboardCriteriaPageViewModel CriteriaPage { get; init; } = new();

    public string? Search { get; init; }

    public string Sort { get; init; } = "name_asc";

    public bool IncludeHidden { get; init; } = true;

    public IReadOnlyList<string> Categories { get; init; } = [];

    public IReadOnlyList<DashboardCriterionViewModel> ExistingCriteria { get; init; } = [];
}

public sealed class AdminClientsViewModel : AdminPageBaseViewModel
{
    public IReadOnlyList<DashboardClientViewModel> Clients { get; init; } = [];

    public string? Search { get; init; }
}

public sealed class AdminAccessViewModel : AdminPageBaseViewModel
{
    public IReadOnlyList<DashboardIdentityUserViewModel> Users { get; init; } = [];

    public string? Search { get; init; }

    public string? Role { get; init; }

    public bool IsCurrentUserSuperAdmin { get; init; }

    public Guid CurrentUserId { get; init; }
}

public sealed class AdminRealtorRequestsViewModel : AdminPageBaseViewModel
{
    public IReadOnlyList<DashboardRealtorRegistrationRequestViewModel> Requests { get; init; } = [];

    public string? Status { get; init; }

    public string? Search { get; init; }
}

public sealed class AdminComplaintsViewModel : AdminPageBaseViewModel
{
    public string? FilterStatus { get; init; }

    public string? FilterCategory { get; init; }

    public string? FilterLink { get; init; }

    public string? FilterDealId { get; init; }

    public string? FilterClientQuery { get; init; }

    public string? FilterRealtorQuery { get; init; }

    public string? FilterDateFrom { get; init; }

    public string? FilterDateTo { get; init; }

    public IReadOnlyList<DashboardComplaintViewModel> Complaints { get; init; } = [];

    public IReadOnlyList<DashboardComplaintViewModel> OpenComplaints { get; init; } = [];
}

public sealed class AdminComplaintDetailsViewModel : AdminPageBaseViewModel
{
    public DashboardComplaintViewModel? Complaint { get; init; }
}

public sealed class AdminDealChatsViewModel : AdminPageBaseViewModel
{
    public string? FilterDealId { get; init; }

    public string? FilterClientQuery { get; init; }

    public string? FilterRealtorQuery { get; init; }

    public IReadOnlyList<Web.Models.DealChat.DealChatSummaryViewModel> Items { get; init; } = [];
}

public sealed class AdminCurrenciesViewModel : AdminPageBaseViewModel
{
    public IReadOnlyList<DashboardCurrencyViewModel> Currencies { get; init; } = [];

    public bool IncludeInactive { get; init; }
}

public sealed class DashboardCriteriaPageViewModel
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public int TotalCount { get; init; }

    public IReadOnlyList<DashboardCriterionViewModel> Items { get; init; } = [];

    public int TotalPages => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

public sealed class DashboardClientViewModel
{
    public Guid Id { get; init; }

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string? MiddleName { get; init; }

    public string PhoneNumber { get; init; } = string.Empty;

    public string? Email { get; init; }

    public DateTime CreatedDate { get; init; }

    public string FullName => string.Join(" ", new[] { LastName, FirstName, MiddleName }
        .Where(x => !string.IsNullOrWhiteSpace(x)));
}

public sealed class DashboardRealtorViewModel
{
    public Guid Id { get; init; }

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string? MiddleName { get; init; }

    public string PhoneNumber { get; init; } = string.Empty;

    public string Level { get; init; } = "Undefined";

    public double AverageRating { get; init; }

    public int DealsThisMonth { get; init; }

    public DateTime CreatedDate { get; init; }
}

public sealed class DashboardCriterionViewModel
{
    public Guid Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string ValueType { get; init; } = "Undefined";

    public string? Category { get; init; }

    public string? Description { get; init; }

    public bool IsHidden { get; init; }

    public IReadOnlyList<DashboardCriterionOptionViewModel> Options { get; init; } = [];

    public DateTime CreatedDate { get; init; }

    public int OptionsCount => Options.Count;

    public string OptionsEditorText => string.Join(Environment.NewLine,
        Options.OrderBy(x => x.SortOrder).ThenBy(x => x.Value)
            .Select(x => $"{x.Value}|{x.Label}|{x.SortOrder}"));
}

public sealed class DashboardComplaintViewModel
{
    public Guid Id { get; init; }

    public Guid ClientId { get; init; }

    public Guid? TargetRealtorId { get; init; }

    public Guid? DealId { get; init; }

    public Guid? PropertyId { get; init; }

    public string ClientFullName { get; init; } = string.Empty;

    public string? ClientPhoneNumber { get; init; }

    public string? ClientEmail { get; init; }

    public string? RealtorFullName { get; init; }

    public string? RealtorPhoneNumber { get; init; }

    public string? RealtorEmail { get; init; }

    public string Category { get; init; } = "Undefined";

    public string Subject { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Status { get; init; } = "Undefined";

    public string ModerationVerdict { get; init; } = "Undefined";

    public string? AdminResolution { get; init; }

    public DateTime? ResolvedAt { get; init; }

    public DateTime CreatedDate { get; init; }
}

public sealed class DashboardHealthViewModel
{
    public string Status { get; init; } = "unknown";

    public bool? Alive { get; init; }

    public DateTime? TimestampUtc { get; init; }

    public string Details { get; init; } = string.Empty;
}

public sealed class DashboardCurrencyViewModel
{
    public Guid Id { get; init; }

    public string Code { get; init; } = "USD";

    public string Name { get; init; } = string.Empty;

    public string Symbol { get; init; } = string.Empty;

    public decimal RateToBase { get; init; }

    public bool IsActive { get; init; }

    public DateTime UpdatedAtUtc { get; init; }

    public DateTime CreatedDate { get; init; }
}

public sealed class DashboardIdentityUserViewModel
{
    public Guid UserId { get; init; }

    public string Email { get; init; } = string.Empty;

    public string? DisplayName { get; init; }

    public bool EmailConfirmed { get; init; }

    public bool IsFrozen { get; init; }

    public IReadOnlyList<string> Roles { get; init; } = [];

    public bool HasRole(string role)
    {
        return Roles.Any(x => string.Equals(x, role, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class DashboardRealtorRegistrationRequestViewModel
{
    public Guid Id { get; init; }

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string? MiddleName { get; init; }

    public string Email { get; init; } = string.Empty;

    public string PhoneNumber { get; init; } = string.Empty;

    public Guid? IdentityUserId { get; init; }

    public string Status { get; init; } = "Pending";

    public string? ReviewComment { get; init; }

    public DateTime? ReviewedAt { get; init; }

    public DateTime CreatedDate { get; init; }

    public string FullName => string.Join(" ", new[] { LastName, FirstName, MiddleName }
        .Where(x => !string.IsNullOrWhiteSpace(x)));
}
