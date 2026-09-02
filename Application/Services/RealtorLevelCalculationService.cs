using Application.Exceptions;
using Application.Interfaces;
using Application.Options;
using Domain.Enums;
using Domain.Primitives;

namespace Application.Services
{
    public sealed class RealtorLevelCalculationService : IRealtorLevelCalculationService
    {
        private readonly IRealtorRepository _realtorRepository;
        private readonly IDealRepository _dealRepository;
        private readonly IRealtorScoreSnapshotRepository _snapshotRepository;
        private readonly IRealtorLevelSettingsService _settingsService;

        public RealtorLevelCalculationService(
            IRealtorRepository realtorRepository,
            IDealRepository dealRepository,
            IRealtorScoreSnapshotRepository snapshotRepository,
            IRealtorLevelSettingsService settingsService)
        {
            _realtorRepository = realtorRepository;
            _dealRepository = dealRepository;
            _snapshotRepository = snapshotRepository;
            _settingsService = settingsService;
        }

        public async Task<RealtorLevel> Recalculate(Guid realtorId)
        {
            var realtor = await _realtorRepository.GetById(realtorId);
            if (realtor is null)
                throw new NotFoundException("Риелтор не найден.");

            if (realtor.IsLevelManuallyAssigned)
                return realtor.Level;

            var settings = _settingsService.GetCurrent();
            var completedDealsCount = await GetAllTimeCompletedDealsCount(realtorId);
            var standardRule = settings.Rules.Single(x => x.Level == RealtorLevel.Standard);

            var nextLevel = RealtorLevel.Junior;
            if (completedDealsCount >= standardRule.MinCompletedDeals)
            {
                var latestSnapshot = await _snapshotRepository.GetLatestByRealtor(realtorId);
                nextLevel = latestSnapshot is null
                    ? RealtorLevel.Junior
                    : CalculateAutomaticLevel(
                        realtor.Level,
                        completedDealsCount,
                        ScoreScaleNormalizer.ToFivePointScale(latestSnapshot.ClientTrustScore),
                        ScoreScaleNormalizer.ToFivePointScale(latestSnapshot.AdminPerformanceScore),
                        settings);
            }

            realtor.SetAutomaticLevel(nextLevel);
            _realtorRepository.Update(realtor);
            return realtor.Level;
        }

        public async Task RecalculateAllAutomatic()
        {
            var realtors = await _realtorRepository.GetActive();
            foreach (var realtor in realtors.Where(x => !x.IsLevelManuallyAssigned))
            {
                await Recalculate(realtor.Id);
            }
        }

        public static RealtorLevel CalculateAutomaticLevel(
            RealtorLevel currentLevel,
            int completedDealsCount,
            double clientTrustScore,
            double adminPerformanceScore,
            RealtorLevelRulesOptions settings)
        {
            var rules = settings.Rules
                .OrderBy(x => x.SortOrder)
                .ToDictionary(x => x.Level);

            var top = rules[RealtorLevel.Top];
            var standard = rules[RealtorLevel.Standard];

            if (MeetsRule(top, completedDealsCount, clientTrustScore, adminPerformanceScore))
                return RealtorLevel.Top;

            if (currentLevel == RealtorLevel.Top
                && MeetsRule(top, completedDealsCount, clientTrustScore, adminPerformanceScore, settings.DemotionBuffer))
            {
                return RealtorLevel.Top;
            }

            if (MeetsRule(standard, completedDealsCount, clientTrustScore, adminPerformanceScore))
                return RealtorLevel.Standard;

            if (currentLevel is RealtorLevel.Standard or RealtorLevel.Top
                && MeetsRule(standard, completedDealsCount, clientTrustScore, adminPerformanceScore, settings.DemotionBuffer))
            {
                return RealtorLevel.Standard;
            }

            return RealtorLevel.Junior;
        }

        private async Task<int> GetAllTimeCompletedDealsCount(Guid realtorId)
        {
            var deals = await _dealRepository.GetByRealtor(realtorId);
            return deals.Count(x => x.Status == DealStatus.Completed);
        }

        private static bool MeetsRule(
            RealtorLevelRuleOptions rule,
            int completedDealsCount,
            double clientTrustScore,
            double adminPerformanceScore,
            double scoreBuffer = 0)
        {
            var minClientTrustScore = Math.Max(0, rule.MinClientTrustScore - scoreBuffer);
            var minAdminPerformanceScore = Math.Max(0, rule.MinAdminPerformanceScore - scoreBuffer);

            return completedDealsCount >= rule.MinCompletedDeals
                   && clientTrustScore >= minClientTrustScore
                   && adminPerformanceScore >= minAdminPerformanceScore;
        }
    }
}
