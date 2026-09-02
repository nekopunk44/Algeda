namespace Web.Models.Properties;

public sealed class PropertyCriterionViewModel
{
    public string DisplayName { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string ValueType { get; init; } = "Undefined";

    public string? Category { get; init; }

    public string Value { get; init; } = string.Empty;

    public string DisplayValue { get; init; } = string.Empty;
}
