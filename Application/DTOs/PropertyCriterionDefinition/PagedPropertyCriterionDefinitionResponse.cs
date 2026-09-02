namespace Application.DTOs.PropertyCriterionDefinition
{
    public record PagedPropertyCriterionDefinitionResponse(
        int Page,
        int PageSize,
        int TotalCount,
        List<PropertyCriterionDefinitionResponse> Items);
}
