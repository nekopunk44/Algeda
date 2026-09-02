using Application.DTOs.RealtorEfficiency;
using Application.Exceptions;
using Application.Interfaces;
using Application.Options;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Application.Services
{
    public sealed class RealtorEligibilitySettingsService : IRealtorEligibilitySettingsService
    {
        private const string SettingKey = "RealtorEligibility";
        private static readonly string[] RequiredLevels = ["Junior", "Standard", "Top"];
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly object _sync = new();
        private readonly ISystemSettingRepository _settingsRepository;
        private EligibilityOptions _current;

        public RealtorEligibilitySettingsService(
            IOptions<RealtorEfficiencyOptions> options,
            ISystemSettingRepository settingsRepository)
        {
            _settingsRepository = settingsRepository;
            _current = LoadCurrent(options.Value.Eligibility);
            EnsureValid(_current);
        }

        public EligibilityOptions GetCurrent()
        {
            lock (_sync)
            {
                return Clone(_current);
            }
        }

        public RealtorEligibilitySettingsResponse GetForAdmin()
        {
            var settings = GetCurrent();
            return new RealtorEligibilitySettingsResponse(
                RestrictionsEnabled: settings.RestrictionsEnabled,
                MinConfirmedHistoryDeals: 0,
                CriticalComplaintLookbackDays: settings.CriticalComplaintLookbackDays,
                ScoreSnapshotMaxAgeHours: settings.ScoreSnapshotMaxAgeHours,
                BlockOnCriticalComplaints: false,
                PriceTiers: settings.PriceTiers
                    .OrderBy(x => x.SortOrder)
                    .Select(x => new RealtorEligibilityTierResponse(
                        x.Name,
                        x.MinPrice,
                        x.MaxPrice,
                        0,
                        0,
                        x.SortOrder))
                    .ToList());
        }

        public void UpdateFromAdmin(UpdateRealtorEligibilitySettingsRequest request)
        {
            var updated = new EligibilityOptions
            {
                RestrictionsEnabled = request.RestrictionsEnabled,
                MinConfirmedHistoryDeals = 0,
                CriticalComplaintLookbackDays = 365,
                ScoreSnapshotMaxAgeHours = 24,
                BlockOnCriticalComplaints = false,
                PriceTiers = request.PriceTiers
                    .Select(x => new EligibilityTierOptions
                    {
                        Name = x.Name,
                        MinPrice = 0m,
                        MaxPrice = string.Equals(x.Name, "Top", StringComparison.OrdinalIgnoreCase) ? null : x.MaxPrice,
                        MinClientTrustScore = 0,
                        MinAdminPerformanceScore = 0,
                        SortOrder = x.SortOrder
                    })
                    .OrderBy(x => x.SortOrder)
                    .ToList()
            };

            EnsureValid(updated);

            lock (_sync)
            {
                _current = Clone(updated);
            }

            _settingsRepository.Upsert(SettingKey, JsonSerializer.Serialize(_current, JsonOptions));
        }

        private EligibilityOptions LoadCurrent(EligibilityOptions fallback)
        {
            var storedValue = _settingsRepository.GetValue(SettingKey);
            if (!string.IsNullOrWhiteSpace(storedValue))
            {
                try
                {
                    var stored = JsonSerializer.Deserialize<EligibilityOptions>(storedValue, JsonOptions);
                    if (stored is not null)
                    {
                        return NormalizeForStartup(Clone(stored));
                    }
                }
                catch (JsonException)
                {
                    return NormalizeForStartup(Clone(fallback));
                }
            }

            return NormalizeForStartup(Clone(fallback));
        }

        private static void EnsureValid(EligibilityOptions options)
        {
            if (options.PriceTiers is null || options.PriceTiers.Count == 0)
                throw new ValidationException("Нужно задать ценовые ограничения для уровней риелторов.");

            var ordered = options.PriceTiers
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.MinPrice)
                .ToList();

            foreach (var requiredLevel in RequiredLevels)
            {
                if (!ordered.Any(x => string.Equals(x.Name, requiredLevel, StringComparison.OrdinalIgnoreCase)))
                    throw new ValidationException($"Не задано ценовое ограничение для уровня {requiredLevel}.");
            }

            for (var i = 0; i < ordered.Count; i++)
            {
                var tier = ordered[i];
                if (string.IsNullOrWhiteSpace(tier.Name))
                    throw new ValidationException("Уровень должен иметь название.");

                if (!RequiredLevels.Any(x => string.Equals(x, tier.Name, StringComparison.OrdinalIgnoreCase)))
                    throw new ValidationException("Можно настраивать только уровни Junior, Standard и Top.");

                if (tier.MinPrice != 0m)
                    throw new ValidationException("Цена от для каждого уровня должна быть 0.");

                if (tier.MaxPrice.HasValue && tier.MaxPrice <= 0m)
                    throw new ValidationException("Цена до должна быть больше 0.");

                var isTop = string.Equals(tier.Name, "Top", StringComparison.OrdinalIgnoreCase);
                if (isTop && tier.MaxPrice.HasValue)
                    throw new ValidationException("Для уровня Top цена до не задается.");

                if (!isTop && !tier.MaxPrice.HasValue)
                    throw new ValidationException("Для уровней Junior и Standard нужно задать цену до.");

                if (i == 0)
                    continue;

                var previous = ordered[i - 1];
                if (previous.MaxPrice.HasValue
                    && tier.MaxPrice.HasValue
                    && tier.MaxPrice.Value <= previous.MaxPrice.Value)
                {
                    throw new ValidationException("Максимальная цена старшего уровня должна быть выше, чем у предыдущего.");
                }
            }
        }

        private static EligibilityOptions Clone(EligibilityOptions options)
        {
            return new EligibilityOptions
            {
                RestrictionsEnabled = options.RestrictionsEnabled,
                MinConfirmedHistoryDeals = 0,
                CriticalComplaintLookbackDays = options.CriticalComplaintLookbackDays,
                ScoreSnapshotMaxAgeHours = options.ScoreSnapshotMaxAgeHours,
                BlockOnCriticalComplaints = false,
                PriceTiers = (options.PriceTiers ?? [])
                    .Select(x => new EligibilityTierOptions
                    {
                        Name = x.Name,
                        MinPrice = x.MinPrice,
                        MaxPrice = x.MaxPrice,
                        MinClientTrustScore = 0,
                        MinAdminPerformanceScore = 0,
                        SortOrder = x.SortOrder
                    })
                    .ToList()
            };
        }

        private static EligibilityOptions NormalizeForStartup(EligibilityOptions options)
        {
            if (options.PriceTiers is null || options.PriceTiers.Count == 0)
            {
                options.PriceTiers = BuildDefaultTiers();
                return NormalizeForStartup(options);
            }

            options.PriceTiers = options.PriceTiers
                .Where(x => RequiredLevels.Any(level => string.Equals(level, x.Name, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.MinPrice)
                .ToList();

            foreach (var missingLevel in RequiredLevels.Where(level =>
                         !options.PriceTiers.Any(x => string.Equals(x.Name, level, StringComparison.OrdinalIgnoreCase))))
            {
                options.PriceTiers.Add(BuildDefaultTier(missingLevel));
            }

            options.PriceTiers = options.PriceTiers
                .OrderBy(x => Array.IndexOf(RequiredLevels, RequiredLevels.First(level => string.Equals(level, x.Name, StringComparison.OrdinalIgnoreCase))))
                .ToList();

            for (var i = 0; i < options.PriceTiers.Count; i++)
            {
                options.PriceTiers[i].SortOrder = i;
                options.PriceTiers[i].MinPrice = 0m;

                if (string.Equals(options.PriceTiers[i].Name, "Top", StringComparison.OrdinalIgnoreCase))
                {
                    options.PriceTiers[i].MaxPrice = null;
                }
            }

            if (HasInvalidStartupRanges(options.PriceTiers))
            {
                options.PriceTiers = BuildDefaultTiers();
            }

            return options;
        }

        private static bool HasInvalidStartupRanges(IReadOnlyList<EligibilityTierOptions> tiers)
        {
            if (tiers.Count != RequiredLevels.Length)
                return true;

            for (var i = 0; i < tiers.Count; i++)
            {
                var tier = tiers[i];
                if (tier.MinPrice != 0m || (tier.MaxPrice.HasValue && tier.MaxPrice.Value <= 0m))
                    return true;

                var isTop = string.Equals(tier.Name, "Top", StringComparison.OrdinalIgnoreCase);
                if (isTop)
                    return tier.MaxPrice.HasValue;

                if (!tier.MaxPrice.HasValue)
                    return true;

                if (i == 0)
                    continue;

                var previous = tiers[i - 1];
                if (previous.MaxPrice.HasValue && tier.MaxPrice.Value <= previous.MaxPrice.Value)
                    return true;
            }

            return false;
        }

        private static List<EligibilityTierOptions> BuildDefaultTiers()
        {
            return RequiredLevels.Select(BuildDefaultTier).ToList();
        }

        private static EligibilityTierOptions BuildDefaultTier(string level)
        {
            return level switch
            {
                "Junior" => new EligibilityTierOptions { Name = "Junior", MinPrice = 0m, MaxPrice = 299999.99m, SortOrder = 0 },
                "Standard" => new EligibilityTierOptions { Name = "Standard", MinPrice = 0m, MaxPrice = 599999.99m, SortOrder = 1 },
                _ => new EligibilityTierOptions { Name = "Top", MinPrice = 0m, MaxPrice = null, SortOrder = 2 }
            };
        }
    }
}
