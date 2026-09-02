using Web.Models.Api;
using Web.Models.RealEstate;

namespace Web.Models.Dashboard;

public sealed class ClientDashboardViewModel
{
    public string? SuccessMessage { get; init; }

    public ApiErrorViewModel? ApiError { get; init; }

    public IReadOnlyList<DashboardReviewViewModel> Reviews { get; init; } = [];

    public IReadOnlyList<DashboardMatchResultViewModel> MatchResults { get; init; } = [];

    public DashboardRequirementViewModel? ActiveRequirement { get; init; }

    public Guid? RequirementId { get; init; }

    public Guid? ClientId { get; init; }

    public int MatchLimit { get; init; } = 20;

    public string SortBy { get; init; } = "score";

    public bool RunAttempted { get; init; }

    public bool CanManageRequirement { get; init; } = true;

    public IReadOnlyList<DashboardNotificationHistoryItemViewModel> NotificationHistory { get; init; } = [];

    public IReadOnlyList<DashboardCriterionDefinitionViewModel> CriterionDefinitions { get; init; } = [];

    public IReadOnlyList<CurrencyRateViewModel> Currencies { get; init; } = [];
}

public sealed class DashboardReviewViewModel
{
    public Guid Id { get; init; }

    public Guid DealId { get; init; }

    public Guid RealtorId { get; init; }

    public Guid ClientId { get; init; }

    public int Score { get; init; }

    public string? Comment { get; init; }

    public DateTime CreatedDate { get; init; }
}

public sealed class DashboardRequirementViewModel
{
    public Guid Id { get; init; }

    public Guid ClientId { get; init; }

    public string DesiredType { get; init; } = "Undefined";

    public IReadOnlyList<string> DesiredTypes { get; init; } = [];

    public double Latitude { get; init; }

    public double Longitude { get; init; }

    public double SearchRadiusMeters { get; init; }

    public bool IgnoreArea { get; init; }

    public decimal MinPrice { get; init; }

    public decimal MaxPrice { get; init; }

    public string PriceCurrency { get; init; } = "USD";

    public double MinArea { get; init; }

    public double? MaxArea { get; init; }

    public string? AddressQuery { get; init; }

    public double MinMatchPercentage { get; init; }

    public double PriceWeight { get; init; }

    public double AreaWeight { get; init; }

    public bool IsActive { get; init; }

    public IReadOnlyList<DashboardRequirementCriterionViewModel> Criteria { get; init; } = [];
}

public sealed class DashboardMatchResultViewModel
{
    public Guid PropertyId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Address { get; init; } = string.Empty;

    public decimal Price { get; init; }

    public decimal OriginalPriceAmount { get; init; }

    public string OriginalPriceCurrency { get; init; } = "USD";

    public double Area { get; init; }

    public int RoomsCount { get; init; }

    public double Latitude { get; init; }

    public double Longitude { get; init; }

    public double DistanceMeters { get; init; }

    public double MatchScore { get; init; }

    public double BaseScore { get; init; }

    public double CriteriaScore { get; init; }

    public bool PassedMustHave { get; init; }
}

public sealed class DashboardRequirementCriterionViewModel
{
    public Guid CriterionDefinitionId { get; init; }

    public string Priority { get; init; } = "Undefined";

    public string? Value { get; init; }

    public IReadOnlyList<string> Values { get; init; } = [];
}

public sealed class DashboardCriterionDefinitionViewModel
{
    public Guid Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string ValueType { get; init; } = "Undefined";

    public string? Category { get; init; }

    public string? Description { get; init; }

    public IReadOnlyList<DashboardCriterionOptionViewModel> Options { get; init; } = [];
}

public sealed class DashboardCriterionOptionViewModel
{
    public string Value { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public int SortOrder { get; init; }
}

public sealed class DashboardNotificationHistoryItemViewModel
{
    public Guid PropertyId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Address { get; init; } = string.Empty;

    public decimal Price { get; init; }

    public double Area { get; init; }

    public DateTime SentAtUtc { get; init; }

    public string NotificationType { get; init; } = string.Empty;
}
