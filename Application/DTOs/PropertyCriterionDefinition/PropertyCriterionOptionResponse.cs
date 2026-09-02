namespace Application.DTOs.PropertyCriterionDefinition
{
    public record PropertyCriterionOptionResponse(
        Guid Id,
        string Value,
        string Label,
        int SortOrder);
}
