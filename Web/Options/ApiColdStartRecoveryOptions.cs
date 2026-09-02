using System.ComponentModel.DataAnnotations;

namespace Web.Options;

public sealed class ApiColdStartRecoveryOptions
{
    public const string SectionName = "ApiColdStartRecovery";

    public bool Enabled { get; init; }

    [Range(1, 10)]
    public int MaxRetryAttempts { get; init; } = 4;

    [Range(1, 60)]
    public int WakeupTimeoutSeconds { get; init; } = 15;

    [Range(0, 60)]
    public int KeepApiAwakeIntervalMinutes { get; init; }
}
