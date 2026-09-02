using Web.Models.Api;
using Web.Models.DealRequests;
using Web.Models.Properties;
using Web.Models.RealtorEfficiency;

namespace Web.Models.DealCenter;

public sealed class DealCenterIndexViewModel
{
    public string? SuccessMessage { get; init; }

    public ApiErrorViewModel? ApiError { get; init; }

    public string Scope { get; init; } = "purchase";

    public string? Search { get; init; }

    public string? Status { get; init; }

    public IReadOnlyList<DealRequestListItemViewModel> Items { get; init; } = [];

    public IReadOnlyDictionary<Guid, int> UnreadMessagesByDealId { get; init; } = new Dictionary<Guid, int>();

    public int GetUnreadCount(Guid dealId)
    {
        return UnreadMessagesByDealId.TryGetValue(dealId, out var count)
            ? Math.Max(0, count)
            : 0;
    }
}

public sealed class DealCenterDetailsViewModel
{
    public string? SuccessMessage { get; init; }

    public ApiErrorViewModel? ApiError { get; init; }

    public DealRequestListItemViewModel? Deal { get; init; }

    public string Scope { get; init; } = "purchase";

    public string? Search { get; init; }

    public string? Status { get; init; }

    public bool IsSale => string.Equals(Deal?.Source, "Sale", StringComparison.OrdinalIgnoreCase);

    public PropertySummaryViewModel? Property { get; init; }

    public DealFeedbackStateViewModel? FeedbackState { get; init; }

    public DealFeedbackFormViewModel FeedbackForm { get; init; } = new();

    public DealComplaintFormViewModel ComplaintForm { get; init; } = new();

    public bool IsFeedbackEligible => FeedbackState?.CanSubmit == true;
}

public sealed class DealFeedbackFormViewModel
{
    public int ServiceScore { get; init; } = 5;

    public int? CommunicationScore { get; init; } = 5;

    public int? ResponsivenessScore { get; init; } = 5;

    public int? ExpertiseScore { get; init; } = 5;

    public int TitleAccuracyScore { get; init; } = 5;

    public int DescriptionAccuracyScore { get; init; } = 5;

    public int PhotosAccuracyScore { get; init; } = 5;

    public int CriteriaAccuracyScore { get; init; } = 5;

    public string? Comment { get; init; }
}

public sealed class DealComplaintFormViewModel
{
    public string Category { get; init; } = "Realtor";

    public string Subject { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public bool IsGeneral { get; init; }
}

public sealed class DealComplaintPageViewModel
{
    public string? SuccessMessage { get; init; }

    public ApiErrorViewModel? ApiError { get; init; }

    public DealRequestListItemViewModel? Deal { get; init; }

    public PropertySummaryViewModel? Property { get; init; }

    public string Source { get; init; } = "deal";

    public string Scope { get; init; } = "purchase";

    public string? Search { get; init; }

    public string? Status { get; init; }

    public Guid? DealId { get; init; }

    public Guid? PropertyId { get; init; }

    public string? Category { get; init; }

    public string Subject { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public IReadOnlyList<ComplaintCategoryOptionViewModel> CategoryOptions { get; init; } = [];

    public bool IsCategoryLocked => CategoryOptions.Count == 1;

    public string BackUrl { get; init; } = "/DealCenter";
}

public sealed class ComplaintCategoryOptionViewModel
{
    public string Value { get; init; } = "Other";

    public string Label { get; init; } = "Другое";
}
