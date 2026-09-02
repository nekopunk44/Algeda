using Web.Models.Api;

namespace Web.Models.Dashboard;

public sealed class AdminDealDocumentAccessLogViewModel
{
    public string? SuccessMessage { get; init; }

    public ApiErrorViewModel? ApiError { get; init; }

    public IReadOnlyList<DealDocumentAccessLogItemViewModel> Items { get; init; } = [];

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 10;

    public int TotalCount { get; init; }

    public int TotalPages { get; init; } = 1;

    public string? DateFrom { get; init; }

    public string? DateTo { get; init; }

    public string? Action { get; init; }

    public string? DealId { get; init; }

    public string? Search { get; init; }
}

public sealed class DealDocumentAccessLogItemViewModel
{
    public Guid Id { get; init; }

    public Guid DealId { get; init; }

    public Guid DealDocumentId { get; init; }

    public string DocumentTitle { get; init; } = string.Empty;

    public string DocumentFileName { get; init; } = string.Empty;

    public string Action { get; init; } = string.Empty;

    public Guid? ActorUserId { get; init; }

    public string ActorDisplayName { get; init; } = string.Empty;

    public string? ActorEmail { get; init; }

    public string ActorRole { get; init; } = string.Empty;

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }

    public DateTime CreatedDate { get; init; }

    public string DealSummary { get; init; } = string.Empty;

    public string ClientSummary { get; init; } = string.Empty;

    public string RealtorSummary { get; init; } = string.Empty;

    public string? PropertyTitle { get; init; }

    public string DealStatus { get; init; } = "Undefined";

    public string? ClientFullName { get; init; }

    public string? ClientPhoneNumber { get; init; }

    public string? ClientEmail { get; init; }

    public string ActorDisplayTitle
    {
        get
        {
            var value = ActorDisplayName.Trim();
            if (string.IsNullOrWhiteSpace(value))
                return "Пользователь";

            if (ActorUserId.HasValue &&
                string.Equals(value, ActorUserId.Value.ToString(), StringComparison.OrdinalIgnoreCase))
                return "Пользователь";

            return Guid.TryParse(value, out _) ? "Пользователь" : value;
        }
    }

    public string ActorText => string.IsNullOrWhiteSpace(ActorEmail)
        ? ActorDisplayName
        : $"{ActorDisplayName} ({ActorEmail})";
}
