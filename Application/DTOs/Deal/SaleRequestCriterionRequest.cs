namespace Application.DTOs.Deal
{
    public record SaleRequestCriterionRequest(
        Guid CriterionDefinitionId,
        string? Value,
        IReadOnlyCollection<string>? Values);
}
