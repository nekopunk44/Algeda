using Domain.Enums;

namespace Application.DTOs.Deal
{
    public record CreateMyDealRequest(
        DealSource Source,
        Guid? PropertyId,
        Guid? ClientRequirementId,
        string? Message);
}
