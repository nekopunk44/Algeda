using System.ComponentModel.DataAnnotations;

namespace API.Auth
{
    public class JwtOptions
    {
        public const string SectionName = "Jwt";

        [Required]
        public string Issuer { get; set; } = string.Empty;

        [Required]
        public string Audience { get; set; } = string.Empty;

        [Required]
        [MinLength(32)]
        public string SigningKey { get; set; } = string.Empty;

        [Range(1, 525600)]
        public int AccessTokenMinutes { get; set; } = 60;
    }
}
