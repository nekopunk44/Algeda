namespace Application.DTOs.Currency
{
    public record CreateCurrencyRateRequest(
        string Code,
        string Name,
        string Symbol,
        decimal RateToBase,
        bool IsActive);
}
