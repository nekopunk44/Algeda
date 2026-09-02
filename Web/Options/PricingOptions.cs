using System.ComponentModel.DataAnnotations;

namespace Web.Options;

public sealed class PricingOptions
{
    public const string SectionName = "Pricing";

    [Required]
    [RegularExpression("^[A-Z]{3}$", ErrorMessage = "BaseCurrencyCode must be a 3-letter ISO code.")]
    public string BaseCurrencyCode { get; init; } = "USD";

    [Required]
    [MaxLength(5)]
    public string CurrencySymbol { get; init; } = "$";

    [Required]
    public string NumberCulture { get; init; } = "en-US";
}
