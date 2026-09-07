using System.ComponentModel.DataAnnotations;

namespace API.Configuration;

public sealed class DatabaseInitializationOptions
{
    public const string SectionName = "DatabaseInitialization";

    public bool ApplyMigrationsOnStartup { get; set; }

    public bool FailOnUnavailable { get; set; } = true;

    public bool FailOnPendingMigrations { get; set; } = true;

    [Range(1, 300)]
    public int StartupTimeoutSeconds { get; set; } = 30;
}
