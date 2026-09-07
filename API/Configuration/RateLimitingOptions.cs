using System.ComponentModel.DataAnnotations;

namespace API.Configuration;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";
    public const string AuthPolicy = "auth";

    [Range(1, 10_000)]
    public int GlobalPermitLimit { get; set; } = 120;

    [Range(1, 3_600)]
    public int GlobalWindowSeconds { get; set; } = 60;

    [Range(1, 1_000)]
    public int AuthPermitLimit { get; set; } = 10;

    [Range(1, 3_600)]
    public int AuthWindowSeconds { get; set; } = 60;
}
