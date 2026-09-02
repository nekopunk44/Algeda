using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Identity
{
    public class IdentitySeedOptions
    {
        public const string SectionName = "IdentitySeed";

        public bool Enabled { get; set; }

        [EmailAddress]
        public string? BootstrapSuperAdminEmail { get; set; }

        [Required]
        public SeedUserOptions Admin { get; set; } = new();

        [Required]
        public SeedUserOptions Realtor { get; set; } = new();

        [Required]
        public SeedUserOptions Client { get; set; } = new();
    }

    public class SeedUserOptions
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(8)]
        public string Password { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? DisplayName { get; set; }
    }
}
