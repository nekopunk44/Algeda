using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.ClientRequirement
{
    public record RequirementResponse(
        Guid Id,
        Guid ClientId,
        PropertyType DesiredType,
        IReadOnlyCollection<PropertyType> DesiredTypes,
        double Latitude,
        double Longitude,
        double SearchRadiusMeters,
        bool IgnoreArea,
        decimal MinPrice,
        decimal MaxPrice,
        double MinArea,
        double? MaxArea,
        string? AddressQuery,
        double PriceWeight,
        double AreaWeight,
        double MinMatchPercentage,
        bool IsActive,
        DateTime CreatedDate,
        IReadOnlyCollection<RequirementCriterionResponse> Criteria);
}
