using System.ComponentModel.DataAnnotations;

namespace Web.Options
{
    public sealed class ApiOptions
    {
        public const string SectionName = "Api";

        [Required]
        public string BaseUrl { get; init; } = "https://localhost:7270";

        [Range(1, 300)]
        public int TimeoutSeconds { get; init; } = 30;
    }
}
