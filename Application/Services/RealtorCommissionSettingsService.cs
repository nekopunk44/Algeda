using Application.DTOs.RealtorEfficiency;
using Application.Exceptions;
using Application.Interfaces;
using Application.Options;
using Domain.Primitives;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Application.Services
{
    public sealed class RealtorCommissionSettingsService : IRealtorCommissionSettingsService
    {
        private const string SettingKey = "RealtorCommissions";

        private static readonly RealtorLevel[] SupportedLevels =
        [
            RealtorLevel.Junior,
            RealtorLevel.Standard,
            RealtorLevel.Top
        ];

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly object _sync = new();
        private readonly ISystemSettingRepository _settingsRepository;
        private readonly List<RealtorLevelCommissionOptions>? _fallbackLevels;
        private Dictionary<RealtorLevel, decimal>? _percents;

        public RealtorCommissionSettingsService(
            IOptions<RealtorCommissionOptions> options,
            ISystemSettingRepository settingsRepository)
        {
            _settingsRepository = settingsRepository;
            _fallbackLevels = options.Value.Levels;
        }

        public decimal GetPercentForLevel(RealtorLevel level)
        {
            lock (_sync)
            {
                var percents = GetLoadedPercents();
                return percents.TryGetValue(level, out var percent)
                    ? percent
                    : percents[RealtorLevel.Junior];
            }
        }

        public RealtorCommissionSettingsResponse GetForAdmin()
        {
            lock (_sync)
            {
                var percents = GetLoadedPercents();
                return new RealtorCommissionSettingsResponse(
                    SupportedLevels
                        .Select(level => new RealtorLevelCommissionResponse(level, percents[level]))
                        .ToList());
            }
        }

        // Настройки читаются из БД лениво, при первом обращении в рамках scope:
        // сервис резолвится на каждый запрос, и запрос в конструкторе бил бы в БД
        // даже там, где комиссии не нужны.
        private Dictionary<RealtorLevel, decimal> GetLoadedPercents()
        {
            return _percents ??= LoadCurrent(_fallbackLevels);
        }

        public void UpdateFromAdmin(UpdateRealtorCommissionSettingsRequest request)
        {
            var updated = Normalize(request.Items.Select(x => new RealtorLevelCommissionOptions
            {
                Level = x.Level,
                Percent = x.Percent
            }));

            var persisted = new RealtorCommissionOptions
            {
                Levels = SupportedLevels
                    .Select(level => new RealtorLevelCommissionOptions
                    {
                        Level = level,
                        Percent = updated[level]
                    })
                    .ToList()
            };

            _settingsRepository.Upsert(
                SettingKey,
                JsonSerializer.Serialize(persisted, JsonOptions));

            lock (_sync)
            {
                _percents = updated;
            }
        }

        private Dictionary<RealtorLevel, decimal> LoadCurrent(
            IEnumerable<RealtorLevelCommissionOptions>? fallback)
        {
            var fallbackValues = Normalize(fallback);
            var storedValue = _settingsRepository.GetValue(SettingKey);
            if (string.IsNullOrWhiteSpace(storedValue))
            {
                return fallbackValues;
            }

            try
            {
                var stored = JsonSerializer.Deserialize<RealtorCommissionOptions>(storedValue, JsonOptions);
                return stored is null
                    ? fallbackValues
                    : Normalize(stored.Levels);
            }
            catch (JsonException)
            {
                return fallbackValues;
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
