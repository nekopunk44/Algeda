using System.ComponentModel.DataAnnotations;

namespace Web.Options;

public sealed class DataProtectionOptions
{
    public const string SectionName = "DataProtection";

    [Required]
    public string ApplicationName { get; set; } = "Algeda.Web";

    public string? KeysPath { get; set; }
}
