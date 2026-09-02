using Web.Models.Api;
using Web.Models.RealtorEfficiency;
using Web.Models.Realtors;

namespace Web.Models.Analytics;

public static class AnalyticsTabs
{
    public const string Agency = "agency";
    public const string Realtors = "realtors";
    public const string Properties = "properties";
    public const string Quality = "quality";

    public static string Normalize(string? raw)
    {
        return raw?.Trim().ToLowerInvariant() switch
        {
            Agency => Agency,
            Properties => Properties,
            Quality => Quality,
            _ => Realtors
        };
    }
}

public sealed class AnalyticsIndexViewModel
{
    public ApiErrorViewModel? ApiError { get; init; }

    public bool IsAdmin { get; init; }

    public string ActiveTab { get; init; } = AnalyticsTabs.Realtors;

    public AnalyticsFilterViewModel Filters { get; init; } = new();

    public AgencyAnalyticsViewModel Agency { get; init; } = new();

    public RealtorAnalyticsPanelViewModel Realtors { get; init; } = new();

    public PropertyAnalyticsViewModel Properties { get; init; } = new();

    public QualityAnalyticsViewModel Quality { get; init; } = new();
}

public sealed class AnalyticsFilterViewModel
{
    public DateOnly? DateFrom { get; init; }

    public DateOnly? DateTo { get; init; }

    public string DealType { get; init; } = "all";

    public string Source { get; init; } = "all";

    public IReadOnlyList<string> AvailableSources { get; init; } = [];
}

public sealed class AgencyAnalyticsViewModel
{
    public int TotalDeals { get; init; }
    public int CompletedDeals { get; init; }
    public int CancelledDeals { get; init; }
    public double ConversionRate { get; init; }
    public decimal TotalCommissionUsd { get; init; }
    public decimal RealtorPayoutUsd { get; init; }
    public decimal AgencyNetCommissionUsd { get; init; }
    public double AverageDealDurationDays { get; init; }
    public int ConfirmedOrPartiallyConfirmedComplaints { get; init; }
    public IReadOnlyList<MonthlyPointViewModel> MonthlyCompletedDeals { get; init; } = [];
}

public sealed class RealtorAnalyticsPanelViewModel
{
    public Guid? SelectedRealtorId { get; init; }

    public IReadOnlyList<RealtorCardViewModel> Realtors { get; init; } = [];

    public RealtorScoreSnapshotViewModel? LatestScore { get; init; }

    public IReadOnlyList<RealtorScoreSnapshotViewModel> ScoreHistory { get; init; } = [];

    public RealtorFeedbackSummaryViewModel? FeedbackSummary { get; init; }
}

public sealed class PropertyAnalyticsViewModel
{
    public int SoldObjectsCount { get; init; }
    public decimal AverageSoldPriceUsd { get; init; }
    public decimal MedianSoldPriceUsd { get; init; }
    public double AverageSaleDurationDays { get; init; }
    public double AverageCardQualityScore { get; init; }
    public IReadOnlyList<NamedCountViewModel> SoldByType { get; init; } = [];
}

public sealed class QualityAnalyticsViewModel
{
    public int OpenComplaints { get; init; }
    public int InProgressComplaints { get; init; }
    public int ResolvedComplaints { get; init; }
    public int ConfirmedComplaints { get; init; }
    public int PartiallyConfirmedComplaints { get; init; }
    public int NotConfirmedComplaints { get; init; }
    public IReadOnlyList<NamedCountViewModel> ComplaintsByCategory { get; init; } = [];
}

public sealed class MonthlyPointViewModel
{
    public string Label { get; init; } = string.Empty;
    public int Value { get; init; }
}

public sealed class NamedCountViewModel
{
    public string Name { get; init; } = string.Empty;
    public int Count { get; init; }
}
