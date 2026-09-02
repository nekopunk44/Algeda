using Domain.Enums;

namespace Application.DTOs.Deal
{
    public record DealResponse(
        Guid Id,
        Guid PropertyId,
        Guid ClientId,
        Guid RealtorId,
        DealStatus Status,
        decimal CommissionAmount,
        string CommissionCurrency,
        decimal RealtorCommissionPercent,
        decimal RealtorPayoutAmount,
        string RealtorPayoutCurrency,
        decimal AgencyNetCommissionAmount,
        string AgencyNetCommissionCurrency,
        Guid? PriorityRealtorId,
        DateTime? PriorityUntilUtc,
        DateTime? CompletedAt,
        DateTime CreatedDate);
}
