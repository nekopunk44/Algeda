using Domain.Enums;

namespace Application.DTOs.ClientRequirement
{
    public record UpdateRequirementRequest(
        Guid Id,
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
        double PriceWeight = 0.6,
        double AreaWeight = 0.4,
        IReadOnlyCollection<RequirementCriterionRequest>? Criteria = null);
}
