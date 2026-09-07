using System.ComponentModel.DataAnnotations;

namespace API.Configuration;

public sealed class AutoMapperLicenseOptions
{
    public const string SectionName = "AutoMapper";

    [Required]
    public string LicenseKey { get; set; } = string.Empty;
}
