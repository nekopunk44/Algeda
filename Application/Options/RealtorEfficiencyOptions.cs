using System.ComponentModel.DataAnnotations;

namespace Application.Options
{
    public class RealtorEfficiencyOptions
    {
        public const string SectionName = "RealtorEfficiency";

        [Range(1, 3650)]
        public int CalculationWindowDays { get; set; } = 90;

        public ClientTrustScoreOptions ClientTrustScore { get; set; } = new();
        public AdminPerformanceScoreOptions AdminPerformanceScore { get; set; } = new();
        public EligibilityOptions Eligibility { get; set; } = new();
        public RealtorLevelRulesOptions LevelRules { get; set; } = new();
        public ComplaintClassificationOptions ComplaintClassification { get; set; } = new();
    }

    public class ClientTrustScoreOptions
    {
        [Range(0, 1)]
        public double ServiceWeight { get; set; } = 0.80;

        [Range(0, 1)]
        public double ComplaintPenaltyWeight { get; set; } = 0.20;

        [Range(0, 5)]
        public double DefaultServiceScore { get; set; } = 0;

        [Range(0, 5)]
        public double DefaultComplaintScore { get; set; } = 0;

        public PenaltyScoreOptions Penalties { get; set; } = new();
    }

    public class AdminPerformanceScoreOptions
    {
        [Range(0, 1)]
        public double PropertyDataQualityWeight { get; set; } = 0.40;

        [Range(0, 1)]
        public double WorkflowDisciplineWeight { get; set; } = 0.20;

        [Range(0, 1)]
        public double BusinessResultWeight { get; set; } = 0.25;

        [Range(0, 1)]
        public double ReputationRiskWeight { get; set; } = 0.15;

        [Range(0, 5)]
        public double DefaultPropertyDataQualityScore { get; set; } = 0;

        [Range(0, 5)]
        public double DefaultWorkflowDisciplineScore { get; set; } = 0;

        [Range(0, 5)]
        public double DefaultBusinessResultScore { get; set; } = 0;

        [Range(0, 5)]
        public double DefaultReputationRiskScore { get; set; } = 0;

        public PropertyDataQualityOptions PropertyDataQuality { get; set; } = new();
        public WorkflowDisciplineOptions WorkflowDiscipline { get; set; } = new();
        public BusinessResultOptions BusinessResult { get; set; } = new();
        public PenaltyScoreOptions ReputationPenalties { get; set; } = new();
    }

    public class PropertyDataQualityOptions
    {
        [Range(1, 20)]
        public int TargetPhotoCount { get; set; } = 8;

        [Range(1, 20)]
        public int TargetCriteriaCount { get; set; } = 6;

        [Range(0, 1)]
        public double OwnerContactWeight { get; set; } = 0.20;

        [Range(0, 1)]
        public double PhotosWeight { get; set; } = 0.20;

        [Range(0, 1)]
        public double CriteriaWeight { get; set; } = 0.20;

        [Range(0, 1)]
        public double ClientVerifiedAccuracyWeight { get; set; } = 0.40;
    }

    public class WorkflowDisciplineOptions
    {
        [Range(1, 100000)]
        public int TargetActivityPoints { get; set; } = 100;

        [Range(0, 1)]
        public double ActivityWeight { get; set; } = 0.60;

        [Range(0, 1)]
        public double CancellationRateWeight { get; set; } = 0.40;
    }

    public class BusinessResultOptions
    {
        [Range(1, 100000)]
        public int TargetCompletedDeals { get; set; } = 10;

        [Range(typeof(decimal), "0.01", "1000000000")]
        public decimal TargetCommission { get; set; } = 100000m;

        [Range(0, 1)]
        public double CompletedDealsWeight { get; set; } = 0.50;

        [Range(0, 1)]
        public double CommissionWeight { get; set; } = 0.50;
    }

    public class PenaltyScoreOptions
    {
        [Range(0, 5)]
        public double PenaltyPerConfirmedComplaint { get; set; } = 0.50;

        [Range(0, 5)]
        public double PenaltyPerCriticalComplaint { get; set; } = 1.50;

        [Range(0, 5)]
        public double PenaltyPerPartiallyConfirmedComplaint { get; set; } = 0.25;

        [Range(0, 5)]
        public double MaxPenalty { get; set; } = 5;
    }

    public class EligibilityOptions
    {
        public bool RestrictionsEnabled { get; set; } = true;

        public List<EligibilityTierOptions> PriceTiers { get; set; } =
        [
            new EligibilityTierOptions
            {
                Name = "Junior",
                MinPrice = 0m,
                MaxPrice = 299999.99m,
                SortOrder = 0
            },
            new EligibilityTierOptions
            {
                Name = "Standard",
                MinPrice = 0m,
                MaxPrice = 599999.99m,
                SortOrder = 1
            },
            new EligibilityTierOptions
            {
                Name = "Top",
                MinPrice = 0m,
                MaxPrice = null,
                SortOrder = 2
            }
        ];

        [Range(0, 1000)]
        public int MinConfirmedHistoryDeals { get; set; } = 3;

        [Range(1, 3650)]
        public int CriticalComplaintLookbackDays { get; set; } = 365;

        [Range(1, 8760)]
        public int ScoreSnapshotMaxAgeHours { get; set; } = 24;

        public bool BlockOnCriticalComplaints { get; set; } = true;
    }

    public class EligibilityTierOptions
    {
        public string Name { get; set; } = "Tier";

        [Range(typeof(decimal), "0", "1000000000")]
        public decimal MinPrice { get; set; } = 300000m;

        [Range(typeof(decimal), "0.01", "1000000000")]
        public decimal? MaxPrice { get; set; }

        [Range(0, 5)]
        public double MinClientTrustScore { get; set; } = 3.50;

        [Range(0, 5)]
        public double MinAdminPerformanceScore { get; set; } = 3.50;

        public int SortOrder { get; set; }

        public bool Matches(decimal price)
        {
            if (price < MinPrice)
            {
                return false;
            }

            return !MaxPrice.HasValue || price <= MaxPrice.Value;
        }
    }

    public class RealtorLevelRulesOptions
    {
        [Range(0, 5)]
        public double DemotionBuffer { get; set; } = 0.30;

        public List<RealtorLevelRuleOptions> Rules { get; set; } =
        [
            new RealtorLevelRuleOptions
            {
                Level = Domain.Primitives.RealtorLevel.Junior,
                MinCompletedDeals = 0,
                MinClientTrustScore = 0,
                MinAdminPerformanceScore = 0,
                SortOrder = 0
            },
            new RealtorLevelRuleOptions
            {
                Level = Domain.Primitives.RealtorLevel.Standard,
                MinCompletedDeals = 10,
                MinClientTrustScore = 3.50,
                MinAdminPerformanceScore = 3.50,
                SortOrder = 1
            },
            new RealtorLevelRuleOptions
            {
                Level = Domain.Primitives.RealtorLevel.Top,
                MinCompletedDeals = 30,
                MinClientTrustScore = 4.30,
                MinAdminPerformanceScore = 4.20,
                SortOrder = 2
            }
        ];
    }

    public class RealtorLevelRuleOptions
    {
        public Domain.Primitives.RealtorLevel Level { get; set; } = Domain.Primitives.RealtorLevel.Junior;

        [Range(0, 100000)]
        public int MinCompletedDeals { get; set; }

        [Range(0, 5)]
        public double MinClientTrustScore { get; set; }

        [Range(0, 5)]
        public double MinAdminPerformanceScore { get; set; }

        public int SortOrder { get; set; }
    }

    public class ComplaintClassificationOptions
    {
        public string[] CriticalKeywords { get; set; } =
        [
            "critical",
            "fraud",
            "threat",
            "\u043c\u043e\u0448\u0435\u043d",
            "\u0443\u0433\u0440\u043e\u0437",
            "\u0448\u0430\u043d\u0442\u0430\u0436"
        ];
    }
}
