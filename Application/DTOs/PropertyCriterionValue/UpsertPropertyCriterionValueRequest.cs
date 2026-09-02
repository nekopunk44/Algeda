namespace Application.DTOs.PropertyCriterionValue
{
    public record UpsertPropertyCriterionValueRequest(
        Guid CriterionDefinitionId,
        string? Value,
        List<string>? Values);
}
