namespace Application.DTOs.Deal
{
    public record CompleteDealRequest(
        Guid DealId,
        decimal CommissionAmount,
        string CommissionCurrency = "USD");
}
