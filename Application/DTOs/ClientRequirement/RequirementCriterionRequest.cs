using Domain.Enums;

namespace Application.DTOs.ClientRequirement
{
    public record RequirementCriterionRequest(
        Guid CriterionDefinitionId,
        RequirementCriterionPriority Priority,
        string? Value,
        IReadOnlyCollection<string>? Values = null);
}
