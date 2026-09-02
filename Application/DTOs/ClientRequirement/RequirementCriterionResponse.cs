using Domain.Enums;

namespace Application.DTOs.ClientRequirement
{
    public record RequirementCriterionResponse(
        Guid CriterionDefinitionId,
        RequirementCriterionPriority Priority,
        string? Value,
        IReadOnlyCollection<string>? Values);
}
