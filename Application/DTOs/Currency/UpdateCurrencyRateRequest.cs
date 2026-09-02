namespace Application.DTOs.Currency
{
    public record UpdateCurrencyRateRequest(
        string Code,
        string Name,
        string Symbol,
        decimal RateToBase,
        bool IsActive);
}
