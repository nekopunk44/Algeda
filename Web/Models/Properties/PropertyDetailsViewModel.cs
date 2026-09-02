using Web.Models.Api;

namespace Web.Models.Properties;

public sealed class PropertyDetailsViewModel
{
    public string? SuccessMessage { get; init; }

    public PropertySummaryViewModel? Property { get; init; }

    public IReadOnlyList<PropertyCriterionViewModel> Criteria { get; init; } = [];

    public ApiErrorViewModel? ApiError { get; init; }

    public string BackUrl { get; init; } = "/#catalog";

    public bool UseHistoryBack { get; init; }
}
