using Domain.Enums;

namespace Application.DTOs.PropertyCriterionValue
{
    public record PropertyCriterionValueResponse(
        Guid Id,
        Guid CriterionDefinitionId,
        string CriterionCode,
        string CriterionDisplayName,
        PropertyCriterionValueType CriterionValueType,
        string? Category,
        string? CriterionDescription,
        bool IsCriterionHidden,
        string Value,
        string DisplayValue,
        DateTime CreatedDate);
}
