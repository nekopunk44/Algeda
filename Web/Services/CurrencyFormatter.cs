using System.Globalization;
using Microsoft.Extensions.Options;
using Web.Models.Pricing;
using Web.Options;

namespace Web.Services;

public interface ICurrencyFormatter
{
    string Format(decimal amount, int fractionDigits = 0);
    string Format(decimal amount, string currencyCode, int fractionDigits = 0);

    PricingClientSettings GetClientSettings();
}

public sealed class CurrencyFormatter : ICurrencyFormatter
{
    private readonly PricingOptions _options;
    private readonly CultureInfo _numberCulture;

    public CurrencyFormatter(IOptions<PricingOptions> options)
    {
        _options = options.Value;
        _numberCulture = CultureInfo.GetCultureInfo(_options.NumberCulture);
    }

    public string Format(decimal amount, int fractionDigits = 0)
    {
        return Format(amount, _options.BaseCurrencyCode, fractionDigits);
    }

    public string Format(decimal amount, string currencyCode, int fractionDigits = 0)
    {
        var digits = Math.Clamp(fractionDigits, 0, 6);
        var numberFormat = (NumberFormatInfo)_numberCulture.NumberFormat.Clone();
        var safeCurrencyCode = string.IsNullOrWhiteSpace(currencyCode)
            ? _options.BaseCurrencyCode
            : currencyCode.Trim().ToUpperInvariant();
        var numericPart = amount.ToString($"N{digits}", numberFormat);

        return $"{numericPart} {safeCurrencyCode}";
    }

    public PricingClientSettings GetClientSettings()
    {
        return new PricingClientSettings
        {
            BaseCurrencyCode = _options.BaseCurrencyCode,
            CurrencySymbol = _options.CurrencySymbol,
            NumberCulture = _options.NumberCulture
        };
    }
}
