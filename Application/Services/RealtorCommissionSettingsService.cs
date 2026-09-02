using Application.DTOs.RealtorEfficiency;
using Application.Exceptions;
using Application.Interfaces;
using Application.Options;
using Domain.Primitives;
using Microsoft.Extensions.Options;

namespace Application.Services
{
    public sealed class RealtorCommissionSettingsService : IRealtorCommissionSettingsService
    {
        private static readonly RealtorLevel[] SupportedLevels =
        [
            RealtorLevel.Junior,
            RealtorLevel.Standard,
            RealtorLevel.Top
        ];

        private readonly object _sync = new();
        private Dictionary<RealtorLevel, decimal> _percents;

        public RealtorCommissionSettingsService(IOptions<RealtorCommissionOptions> options)
        {
            _percents = Normalize(options.Value.Levels);
        }

        public decimal GetPercentForLevel(RealtorLevel level)
        {
            lock (_sync)
            {
                return _percents.TryGetValue(level, out var percent)
                    ? percent
                    : _percents[RealtorLevel.Junior];
            }
        }

        public RealtorCommissionSettingsResponse GetForAdmin()
        {
            lock (_sync)
            {
                return new RealtorCommissionSettingsResponse(
                    SupportedLevels
                        .Select(level => new RealtorLevelCommissionResponse(level, _percents[level]))
                        .ToList());
            }
        }

        public void UpdateFromAdmin(UpdateRealtorCommissionSettingsRequest request)
        {
            var updated = Normalize(request.Items.Select(x => new RealtorLevelCommissionOptions
            {
                Level = x.Level,
                Percent = x.Percent
            }));

            lock (_sync)
            {
                _percents = updated;
            }
        }

        private static Dictionary<RealtorLevel, decimal> Normalize(
            IEnumerable<RealtorLevelCommissionOptions>? source)
        {
            var result = SupportedLevels.ToDictionary(
                level => level,
                level => level switch
                {
                    RealtorLevel.Top => 40m,
                    RealtorLevel.Standard => 30m,
                    _ => 20m
                });

            foreach (var item in source ?? [])
            {
                if (!SupportedLevels.Contains(item.Level))
                {
                    continue;
                }

                if (item.Percent < 0 || item.Percent > 100)
                {
                    throw new ValidationException("Процент выплаты риелтору должен быть в диапазоне от 0 до 100.");
                }

                result[item.Level] = decimal.Round(item.Percent, 2, MidpointRounding.AwayFromZero);
            }

            return result;
        }
    }
}
