using Web.Models.Api;
using Web.Models.RealEstate;

namespace Web.Models.Properties;

public sealed class HomeMapViewModel
{
    public bool IsAuthenticated { get; init; }

    public IReadOnlyList<PropertySummaryViewModel> Properties { get; init; } = [];

    public IReadOnlyList<CurrencyRateViewModel> Currencies { get; init; } = [];

    public ApiErrorViewModel? ApiError { get; init; }
}
