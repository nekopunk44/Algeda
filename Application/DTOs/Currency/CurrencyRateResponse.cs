namespace Application.DTOs.Currency
{
    public record CurrencyRateResponse(
        Guid Id,
        string Code,
        string Name,
        string Symbol,
        decimal RateToBase,
        bool IsActive,
        DateTime UpdatedAtUtc,
        DateTime CreatedDate);
}
