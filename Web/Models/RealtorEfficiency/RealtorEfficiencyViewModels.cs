using Web.Models.Api;
using Web.Models.Dashboard;
using Web.Models.Realtors;

namespace Web.Models.RealtorEfficiency;

public sealed class ClientFeedbackCandidateViewModel
{
    public Guid DealId { get; init; }

    public string PropertyTitle { get; init; } = string.Empty;

    public string RealtorDisplayName { get; init; } = string.Empty;

    public DateTime CreatedDate { get; init; }
}

public sealed class RealtorScoreSnapshotViewModel
{
    public Guid SnapshotId { get; init; }

    public Guid RealtorId { get; init; }

    public double ClientTrustScore { get; init; }

    public double AdminPerformanceScore { get; init; }

    public ClientTrustBreakdownViewModel ClientTrustBreakdown { get; init; } = new();

    public AdminPerformanceBreakdownViewModel AdminPerformanceBreakdown { get; init; } = new();

    public string? CalculationVersion { get; init; }

    public DateTime CreatedDate { get; init; }
}

public sealed class ClientTrustBreakdownViewModel
{
    public double ClientServiceScoreComponent { get; init; }

    public double ComplaintPenaltyComponent { get; init; }
}

public sealed class AdminPerformanceBreakdownViewModel
{
    public double PropertyDataQualityComponent { get; init; }

    public double WorkflowDisciplineComponent { get; init; }

    public double BusinessResultComponent { get; init; }

    public double ReputationRiskComponent { get; init; }
}

public sealed class RealtorBlockReasonViewModel
{
    public string Code { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public bool IsBlockingNow { get; init; }
}

public sealed class RealtorComplaintStatsViewModel
{
    public int TotalCount { get; init; }

    public int OpenCount { get; init; }

    public int InProgressCount { get; init; }

    public int ResolvedCount { get; init; }

    public int ConfirmedCount { get; init; }

    public int PartiallyConfirmedCount { get; init; }

    public int NotConfirmedCount { get; init; }

    public IReadOnlyList<DashboardComplaintViewModel> RecentComplaints { get; init; } = [];
}

public sealed class AdminRealtorAnalyticsViewModel : AdminPageBaseViewModel
{
    public IReadOnlyList<RealtorCardViewModel> Realtors { get; init; } = [];

    public int RealtorsPage { get; init; } = 1;

    public int RealtorsTotalPages { get; init; } = 1;

    public int RealtorsTotalCount { get; init; }

    public string? Search { get; init; }

    public string? Level { get; init; }

    public Guid? SelectedRealtorId { get; init; }

    public RealtorCardViewModel? SelectedRealtor { get; init; }

    public RealtorScoreSnapshotViewModel? LatestScore { get; init; }

    public IReadOnlyList<RealtorScoreSnapshotViewModel> ScoreHistory { get; init; } = [];

    public int ScoreHistoryPage { get; init; } = 1;

    public int ScoreHistoryTotalPages { get; init; } = 1;

    public int ScoreHistoryTotalCount { get; init; }

    public IReadOnlyList<RealtorBlockReasonViewModel> BlockReasons { get; init; } = [];

    public double? ClientTrustTrendDelta { get; init; }

    public double? AdminPerformanceTrendDelta { get; init; }

    public int OpenComplaintsCountForRealtor { get; init; }

    public RealtorComplaintStatsViewModel ComplaintStats { get; init; } = new();

    public RealtorFeedbackSummaryViewModel? FeedbackSummary { get; init; }

    public int FeedbackPage { get; init; } = 1;

    public int FeedbackTotalPages { get; init; } = 1;

    public int FeedbackTotalCount { get; init; }

    public RealtorEligibilitySettingsViewModel? EligibilitySettings { get; init; }
}

public sealed class AdminRealtorCommissionSettingsViewModel : AdminPageBaseViewModel
{
    public RealtorCommissionSettingsViewModel Settings { get; init; } = new();
}

public sealed class AdminSystemSettingsViewModel : AdminPageBaseViewModel
{
    public RealtorEligibilitySettingsViewModel EligibilitySettings { get; init; } = new();

    public RealtorCommissionSettingsViewModel CommissionSettings { get; init; } = new();

    public RealtorLevelSettingsViewModel LevelSettings { get; init; } = new();
}

public sealed class RealtorImprovementHintViewModel
{
    public string Severity { get; init; } = "Info";

    public string Title { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}

public sealed class DealFeedbackStateViewModel
{
    public Guid DealId { get; init; }

    public string FormType { get; init; } = "Undefined";

    public bool IsCompleted { get; init; }

    public bool IsSubmitted { get; init; }

    public bool CanSubmit { get; init; }

    public DateTime? CompletedAtUtc { get; init; }

    public DateTime? DeadlineUtc { get; init; }

    public int? DaysRemaining { get; init; }

    public string? BlockReasonCode { get; init; }

    public string? BlockReason { get; init; }

    public bool IsPurchase => string.Equals(FormType, "Purchase", StringComparison.OrdinalIgnoreCase);

    public bool IsSale => string.Equals(FormType, "Sale", StringComparison.OrdinalIgnoreCase);
}

public sealed class RealtorFeedbackSummaryViewModel
{
    public Guid RealtorId { get; init; }
    public int TotalFeedbackCount { get; init; }
    public int ServiceFeedbackCount { get; init; }
    public int PropertyFeedbackCount { get; init; }
    public int PurchaseFeedbackCount { get; init; }
    public int SaleFeedbackCount { get; init; }
    public double? AverageServiceScore { get; init; }
    public double? AverageCommunicationScore { get; init; }
    public double? AverageResponsivenessScore { get; init; }
    public double? AverageExpertiseScore { get; init; }
    public double? AverageTitleAccuracyScore { get; init; }
    public double? AverageDescriptionAccuracyScore { get; init; }
    public double? AveragePhotosAccuracyScore { get; init; }
    public double? AverageCriteriaAccuracyScore { get; init; }
    public IReadOnlyList<RealtorFeedbackSummaryItemViewModel> RecentItems { get; init; } = [];
}

public sealed class RealtorFeedbackSummaryItemViewModel
{
    public Guid FeedbackId { get; init; }
    public Guid DealId { get; init; }
    public string FormType { get; init; } = "Undefined";
    public bool IsServiceFeedback { get; init; }
    public bool IsPropertyFeedback { get; init; }
    public int ServiceScore { get; init; }
    public int? CommunicationScore { get; init; }
    public int? ResponsivenessScore { get; init; }
    public int? ExpertiseScore { get; init; }
    public int? TitleAccuracyScore { get; init; }
    public int? DescriptionAccuracyScore { get; init; }
    public int? PhotosAccuracyScore { get; init; }
    public int? CriteriaAccuracyScore { get; init; }
    public string? Comment { get; init; }
    public DateTime CreatedDate { get; init; }
}

public sealed class RealtorEligibilitySettingsViewModel
{
    public bool RestrictionsEnabled { get; init; }
    public int MinConfirmedHistoryDeals { get; init; }
    public int CriticalComplaintLookbackDays { get; init; }
    public int ScoreSnapshotMaxAgeHours { get; init; }
    public bool BlockOnCriticalComplaints { get; init; }
    public IReadOnlyList<RealtorEligibilityTierViewModel> PriceTiers { get; init; } = [];
}

public sealed class RealtorEligibilityTierViewModel
{
    public string Name { get; init; } = string.Empty;
    public decimal MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public double MinClientTrustScore { get; init; }
    public double MinAdminPerformanceScore { get; init; }
    public int SortOrder { get; init; }
}

public sealed class RealtorCommissionSettingsViewModel
{
    public IReadOnlyList<RealtorLevelCommissionViewModel> Items { get; init; } = [];
}

public sealed class RealtorLevelCommissionViewModel
{
    public string Level { get; init; } = "Undefined";
    public decimal Percent { get; init; }
}

public sealed class RealtorLevelSettingsViewModel
{
    public double DemotionBuffer { get; init; } = 0.30;

    public IReadOnlyList<RealtorLevelRuleViewModel> Rules { get; init; } =
    [
        new RealtorLevelRuleViewModel
        {
            Level = "Junior",
            MinCompletedDeals = 0,
            MinClientTrustScore = 0,
            MinAdminPerformanceScore = 0,
            SortOrder = 0
        },
        new RealtorLevelRuleViewModel
        {
            Level = "Standard",
            MinCompletedDeals = 10,
            MinClientTrustScore = 3.50,
            MinAdminPerformanceScore = 3.50,
            SortOrder = 1
        },
        new RealtorLevelRuleViewModel
        {
            Level = "Top",
            MinCompletedDeals = 30,
            MinClientTrustScore = 4.30,
            MinAdminPerformanceScore = 4.20,
            SortOrder = 2
        }
    ];
}

public sealed class RealtorLevelRuleViewModel
{
    public string Level { get; init; } = "Undefined";

    public int MinCompletedDeals { get; init; }

    public double MinClientTrustScore { get; init; }

    public double MinAdminPerformanceScore { get; init; }

    public int SortOrder { get; init; }
}
