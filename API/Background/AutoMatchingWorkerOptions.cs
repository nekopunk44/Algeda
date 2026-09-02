using System.ComponentModel.DataAnnotations;

namespace API.Background
{
    public sealed class AutoMatchingWorkerOptions
    {
        public const string SectionName = "AutoMatchingWorker";

        public bool Enabled { get; set; }

        [Range(1, 86400)]
        public int PollIntervalSeconds { get; set; } = 30;

        [Range(1, 5000)]
        public int BatchSize { get; set; } = 100;

        [Range(1, 5000)]
        public int RequirementsLimit { get; set; } = 500;

        [Range(1, 10080)]
        public int TrackedPropertiesTtlMinutes { get; set; } = 1440;
    }
}
