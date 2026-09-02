using Web.Models.Api;
using Web.Models.Properties;
using Web.Models.RealtorEfficiency;

namespace Web.Models.Dashboard;

public sealed class RealtorDashboardViewModel
{
    public string? SuccessMessage { get; init; }

    public ApiErrorViewModel? ApiError { get; init; }

    public IReadOnlyList<PropertySummaryViewModel> Properties { get; init; } = [];

    public IReadOnlyList<DashboardDealViewModel> Deals { get; init; } = [];

    public IReadOnlyList<DashboardActivityLogViewModel> ActivityLogs { get; init; } = [];

    public Guid? DealsByRealtorId { get; init; }

    public Guid? DealsByClientId { get; init; }

    public Guid? ActivityByRealtorId { get; init; }

    public DashboardPropertySubscribersViewModel? SubscribersInfo { get; init; }

    public IReadOnlyList<RealtorImprovementHintViewModel> ImprovementHints { get; init; } = [];
}

public sealed class DashboardDealViewModel
{
    public Guid Id { get; init; }

    public Guid PropertyId { get; init; }

    public Guid ClientId { get; init; }

    public Guid RealtorId { get; init; }

    public string Status { get; init; } = "Undefined";

    public decimal CommissionAmount { get; init; }

    public string CommissionCurrency { get; init; } = "USD";

    public decimal RealtorCommissionPercent { get; init; }

    public decimal RealtorPayoutAmount { get; init; }

    public string RealtorPayoutCurrency { get; init; } = "USD";

    public decimal AgencyNetCommissionAmount { get; init; }

    public string AgencyNetCommissionCurrency { get; init; } = "USD";

    public DateTime? CompletedAt { get; init; }

    public DateTime CreatedDate { get; init; }
}

public sealed class DashboardActivityLogViewModel
{
    public Guid Id { get; init; }

    public Guid RealtorId { get; init; }

    public string Type { get; init; } = "Undefined";

    public int Points { get; init; }

    public DateTime CreatedDate { get; init; }
}

public sealed class DashboardPropertySubscribersViewModel
{
    public Guid PropertyId { get; init; }

    public int ClientsCount { get; init; }
}
