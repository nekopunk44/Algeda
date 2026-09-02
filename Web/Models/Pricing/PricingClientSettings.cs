namespace Web.Models.Pricing;

public sealed class PricingClientSettings
{
    public string BaseCurrencyCode { get; init; } = "USD";

    public string CurrencySymbol { get; init; } = "$";

    public string NumberCulture { get; init; } = "en-US";
}
