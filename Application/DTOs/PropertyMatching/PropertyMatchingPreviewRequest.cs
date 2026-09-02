using Domain.Enums;

namespace Application.DTOs.PropertyMatching
{
    public record PropertyMatchingPreviewRequest(
        PropertyType DesiredType,
        IReadOnlyCollection<PropertyType>? DesiredTypes,
        double Latitude,
        double Longitude,
        double SearchRadiusMeters,
        bool IgnoreArea,
        decimal MinPrice,
        decimal MaxPrice,
        double MinArea,
        double? MaxArea,
        string? AddressQuery,
        double MinMatchPercentage,
        IReadOnlyCollection<PropertyMatchingPreviewCriterionRequest>? Criteria,
        int Limit = 20);

    public record PropertyMatchingPreviewCriterionRequest(
        Guid CriterionDefinitionId,
        RequirementCriterionPriority Priority,
        string? Value,
        IReadOnlyCollection<string>? Values);
}
