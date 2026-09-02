using Web.Models.Api;
using Web.Models.Dashboard;
using Web.Models.Properties;

namespace Web.Models.DealRequests;

public sealed class DealRequestsIndexViewModel
{
    public string? SuccessMessage { get; init; }

    public ApiErrorViewModel? ApiError { get; init; }

    public DealRequestFilterViewModel Filters { get; init; } = new();

    public IReadOnlyList<DealRequestListItemViewModel> Items { get; init; } = [];

    public IReadOnlyList<DealRequestStatusCountViewModel> StatusCounts { get; init; } = [];

    public int TotalCount { get; init; }

    public int TotalPages { get; init; }

    public bool IsAdmin { get; init; }

    public IReadOnlyDictionary<Guid, int> UnreadMessagesByDealId { get; init; } = new Dictionary<Guid, int>();

    public int GetUnreadCount(Guid dealId)
    {
        return UnreadMessagesByDealId.TryGetValue(dealId, out var count)
            ? Math.Max(0, count)
            : 0;
    }
}

public sealed class DealRequestFilterViewModel
{
    public string Scope { get; init; } = "incoming";

    public string? Source { get; init; }

    public string? Search { get; init; }

    public string? Status { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}

public sealed class DealRequestStatusCountViewModel
{
    public string Status { get; init; } = "Undefined";

    public int Count { get; init; }
}

public sealed class DealRequestListItemViewModel
{
    public Guid Id { get; init; }

    public Guid ClientId { get; init; }

    public string ClientFullName { get; init; } = string.Empty;

    public string ClientPhoneNumber { get; init; } = string.Empty;

    public string? ClientEmail { get; init; }

    public Guid? PropertyId { get; init; }

    public string? PropertyTitle { get; init; }

    public Guid? ClientRequirementId { get; init; }

    public string Source { get; init; } = "Undefined";

    public string Status { get; init; } = "Undefined";

    public bool IsIncoming { get; init; }

    public Guid? RealtorId { get; init; }

    public string? RealtorFullName { get; init; }

    public string? RealtorPhoneNumber { get; init; }

    public string? RealtorEmail { get; init; }

    public string? RequestMessage { get; init; }

    public DateTime? AcceptedAtUtc { get; init; }

    public DateTime? RejectedAtUtc { get; init; }

    public Guid? PriorityRealtorId { get; init; }

    public DateTime? PriorityUntilUtc { get; init; }

    public DateTime? CompletedAtUtc { get; init; }

    public IReadOnlyList<DealRequestNoteViewModel> Notes { get; init; } = [];

    public DateTime CreatedDate { get; init; }
}

public sealed class DealRequestNoteViewModel
{
    public Guid Id { get; init; }

    public Guid? AuthorRealtorId { get; init; }

    public string Text { get; init; } = string.Empty;

    public DateTime? UpdatedAtUtc { get; init; }

    public DateTime CreatedDate { get; init; }
}

public sealed class DealRequestDetailsViewModel
{
    public string? SuccessMessage { get; init; }

    public ApiErrorViewModel? ApiError { get; init; }

    public DealRequestListItemViewModel? Deal { get; init; }

    public DashboardRequirementViewModel? Requirement { get; init; }

    public PropertySummaryViewModel? Property { get; init; }

    public IReadOnlyList<DealDocumentViewModel> Documents { get; init; } = [];

    public IReadOnlyList<DealRequestRealtorCandidateViewModel> RealtorCandidates { get; init; } = [];

    public string? RealtorSearch { get; init; }

    public bool IsAdmin { get; init; }

    public bool IsRealtor { get; init; }

    public string ReturnScope { get; init; } = "incoming";

    public string? ReturnSource { get; init; }

    public string? ReturnSearch { get; init; }

    public string? ReturnStatus { get; init; }

    public int ReturnPage { get; init; } = 1;

    public int ReturnPageSize { get; init; } = 20;
}

public sealed class DealDocumentViewModel
{
    public Guid Id { get; init; }

    public Guid DealId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string OriginalFileName { get; init; } = string.Empty;

    public string ContentType { get; init; } = string.Empty;

    public long FileSizeBytes { get; init; }

    public Guid? UploadedByUserId { get; init; }

    public string UploadedByDisplayName { get; init; } = string.Empty;

    public string? UploadedByEmail { get; init; }

    public DateTime CreatedDate { get; init; }

    public string UploadedByText => string.IsNullOrWhiteSpace(UploadedByEmail)
        ? UploadedByDisplayName
        : $"{UploadedByDisplayName} ({UploadedByEmail})";
}

public sealed class DealDocumentFileViewModel
{
    public string FileName { get; init; } = string.Empty;

    public string ContentType { get; init; } = "application/octet-stream";

    public byte[] Content { get; init; } = [];
}

public sealed class DealRequestRealtorCandidateViewModel
{
    public Guid RealtorId { get; init; }

    public string FullName { get; init; } = string.Empty;

    public string PhoneNumber { get; init; } = string.Empty;

    public string? Email { get; init; }

    public string Display => string.IsNullOrWhiteSpace(Email)
        ? $"{FullName} | {PhoneNumber}"
        : $"{FullName} | {PhoneNumber} | {Email}";
}
