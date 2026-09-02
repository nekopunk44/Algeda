using Domain.Enums;

namespace Application.DTOs.PropertyCriterionDefinition
{
    public record UpdatePropertyCriterionDefinitionRequest(
        string Code,
        string DisplayName,
        PropertyCriterionValueType ValueType,
        string? Category,
        string? Description,
        bool IsHidden,
        List<PropertyCriterionOptionRequest>? Options);
}
