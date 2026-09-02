using System.ComponentModel.DataAnnotations;
using Domain.Primitives;

namespace Application.Options
{
    public sealed class RealtorCommissionOptions
    {
        public const string SectionName = "RealtorCommissions";

        public List<RealtorLevelCommissionOptions> Levels { get; set; } =
        [
            new RealtorLevelCommissionOptions { Level = RealtorLevel.Junior, Percent = 20m },
            new RealtorLevelCommissionOptions { Level = RealtorLevel.Standard, Percent = 30m },
            new RealtorLevelCommissionOptions { Level = RealtorLevel.Top, Percent = 40m }
        ];
    }

    public sealed class RealtorLevelCommissionOptions
    {
        public RealtorLevel Level { get; set; }

        [Range(typeof(decimal), "0", "100")]
        public decimal Percent { get; set; }
    }
}
