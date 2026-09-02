using Domain.Enums;

namespace Application.DTOs.PropertyCriterionDefinition
{
    public record PropertyCriterionDefinitionResponse(
        Guid Id,
        string Code,
        string DisplayName,
        PropertyCriterionValueType ValueType,
        string? Category,
        string? Description,
        bool IsHidden,
        List<PropertyCriterionOptionResponse> Options,
        DateTime CreatedDate);
}
