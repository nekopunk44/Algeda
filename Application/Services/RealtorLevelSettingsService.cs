using Application.DTOs.RealtorEfficiency;
using Application.Exceptions;
using Application.Interfaces;
using Application.Options;
using Domain.Primitives;
using Microsoft.Extensions.Options;

namespace Application.Services
{
    public sealed class RealtorLevelSettingsService : IRealtorLevelSettingsService
    {
        private static readonly RealtorLevel[] RequiredLevels =
        [
            RealtorLevel.Junior,
            RealtorLevel.Standard,
            RealtorLevel.Top
        ];

        private readonly object _sync = new();
        private RealtorLevelRulesOptions _current;

        public RealtorLevelSettingsService(IOptions<RealtorEfficiencyOptions> options)
        {
            _current = Normalize(Clone(options.Value.LevelRules));
            EnsureValid(_current);
        }

        public RealtorLevelRulesOptions GetCurrent()
        {
            lock (_sync)
            {
                return Clone(_current);
            }
        }

        public RealtorLevelSettingsResponse GetForAdmin()
        {
            var settings = GetCurrent();
            return new RealtorLevelSettingsResponse(
                DemotionBuffer: settings.DemotionBuffer,
                Rules: settings.Rules
                    .OrderBy(x => x.SortOrder)
                    .Select(x => new RealtorLevelRuleResponse(
                        x.Level,
                        x.MinCompletedDeals,
                        x.MinClientTrustScore,
                        x.MinAdminPerformanceScore,
                        x.SortOrder))
                    .ToList());
        }

        public void UpdateFromAdmin(UpdateRealtorLevelSettingsRequest request)
        {
            var updated = new RealtorLevelRulesOptions
            {
                DemotionBuffer = request.DemotionBuffer,
                Rules = (request.Rules ?? [])
                    .Select(x => new RealtorLevelRuleOptions
                    {
                        Level = x.Level,
                        MinCompletedDeals = x.Level == RealtorLevel.Junior ? 0 : x.MinCompletedDeals,
                        MinClientTrustScore = x.Level == RealtorLevel.Junior ? 0 : x.MinClientTrustScore,
                        MinAdminPerformanceScore = x.Level == RealtorLevel.Junior ? 0 : x.MinAdminPerformanceScore,
                        SortOrder = x.SortOrder
                    })
                    .ToList()
            };

            updated = Normalize(updated);
            EnsureValid(updated);

            lock (_sync)
            {
                _current = Clone(updated);
            }
        }

        private static RealtorLevelRulesOptions Normalize(RealtorLevelRulesOptions options)
        {
            options.Rules ??= [];
            options.Rules = options.Rules
                .Where(x => RequiredLevels.Contains(x.Level))
                .GroupBy(x => x.Level)
                .Select(x => x.OrderBy(rule => rule.SortOrder).First())
                .ToList();

            foreach (var level in RequiredLevels.Where(level => options.Rules.All(x => x.Level != level)))
            {
                options.Rules.Add(BuildDefaultRule(level));
            }

            options.Rules = options.Rules
                .OrderBy(x => Array.IndexOf(RequiredLevels, x.Level))
                .ToList();

            for (var i = 0; i < options.Rules.Count; i++)
            {
                options.Rules[i].SortOrder = i;
                if (options.Rules[i].Level == RealtorLevel.Junior)
                {
                    options.Rules[i].MinCompletedDeals = 0;
                    options.Rules[i].MinClientTrustScore = 0;
                    options.Rules[i].MinAdminPerformanceScore = 0;
                }
            }

            return options;
        }

        private static void EnsureValid(RealtorLevelRulesOptions options)
        {
            if (options.DemotionBuffer is < 0 or > 5)
                throw new ValidationException("Буфер понижения уровня должен быть от 0.00 до 5.00.");

            if (options.Rules is null || options.Rules.Count == 0)
                throw new ValidationException("Нужно задать правила уровней риелторов.");

            foreach (var level in RequiredLevels)
            {
                if (options.Rules.Count(x => x.Level == level) != 1)
                    throw new ValidationException($"Нужно задать ровно одно правило для уровня \"{GetLevelDisplayName(level)}\".");
            }

            foreach (var rule in options.Rules)
            {
                if (!RequiredLevels.Contains(rule.Level))
                    throw new ValidationException("Можно настраивать только младший, стандартный и топ-уровень риелторов.");

                if (rule.MinCompletedDeals < 0)
                    throw new ValidationException("Минимальное количество завершенных сделок не может быть отрицательным.");

                if (rule.MinClientTrustScore is < 0 or > 5 || rule.MinAdminPerformanceScore is < 0 or > 5)
                    throw new ValidationException("CTS и APS должны быть от 0.00 до 5.00.");
            }

            var junior = options.Rules.Single(x => x.Level == RealtorLevel.Junior);
            if (junior.MinCompletedDeals != 0 || junior.MinClientTrustScore != 0 || junior.MinAdminPerformanceScore != 0)
                throw new ValidationException("Младший уровень является базовым и не должен иметь пороги.");

            var standard = options.Rules.Single(x => x.Level == RealtorLevel.Standard);
            var top = options.Rules.Single(x => x.Level == RealtorLevel.Top);

            if (standard.MinCompletedDeals <= 0)
                throw new ValidationException("Для стандартного уровня нужно указать минимум завершенных сделок больше 0.");

            if (top.MinCompletedDeals <= standard.MinCompletedDeals)
                throw new ValidationException("Минимум сделок для топ-уровня должен быть больше, чем для стандартного уровня.");

            if (top.MinClientTrustScore < standard.MinClientTrustScore)
                throw new ValidationException("Минимальный CTS для топ-уровня не может быть ниже, чем для стандартного уровня.");

            if (top.MinAdminPerformanceScore < standard.MinAdminPerformanceScore)
                throw new ValidationException("Минимальный APS для топ-уровня не может быть ниже, чем для стандартного уровня.");
        }

        private static string GetLevelDisplayName(RealtorLevel level)
        {
            return level switch
            {
                RealtorLevel.Junior => "младший",
                RealtorLevel.Standard => "стандартный",
                RealtorLevel.Top => "топ",
                _ => level.ToString()
            };
        }

        private static RealtorLevelRulesOptions Clone(RealtorLevelRulesOptions options)
        {
            return new RealtorLevelRulesOptions
            {
                DemotionBuffer = options.DemotionBuffer,
                Rules = (options.Rules ?? [])
                    .Select(x => new RealtorLevelRuleOptions
                    {
                        Level = x.Level,
                        MinCompletedDeals = x.MinCompletedDeals,
                        MinClientTrustScore = x.MinClientTrustScore,
                        MinAdminPerformanceScore = x.MinAdminPerformanceScore,
                        SortOrder = x.SortOrder
                    })
                    .ToList()
            };
        }

        private static RealtorLevelRuleOptions BuildDefaultRule(RealtorLevel level)
        {
            return level switch
            {
                RealtorLevel.Standard => new RealtorLevelRuleOptions
                {
                    Level = RealtorLevel.Standard,
                    MinCompletedDeals = 10,
                    MinClientTrustScore = 3.50,
                    MinAdminPerformanceScore = 3.50,
                    SortOrder = 1
                },
                RealtorLevel.Top => new RealtorLevelRuleOptions
                {
                    Level = RealtorLevel.Top,
                    MinCompletedDeals = 30,
                    MinClientTrustScore = 4.30,
                    MinAdminPerformanceScore = 4.20,
                    SortOrder = 2
                },
                _ => new RealtorLevelRuleOptions
                {
                    Level = RealtorLevel.Junior,
                    MinCompletedDeals = 0,
                    MinClientTrustScore = 0,
                    MinAdminPerformanceScore = 0,
                    SortOrder = 0
                }
            };
        }
    }
}
