namespace Application.DTOs.PropertyCriterionDefinition
{
    public record PropertyCriterionOptionRequest(
        string Value,
        string Label,
        int SortOrder = 0);
}
