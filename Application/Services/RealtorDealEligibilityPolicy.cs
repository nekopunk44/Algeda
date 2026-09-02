using Application.DTOs.RealtorEfficiency;
using Application.Options;

namespace Application.Services
{
    public sealed class RealtorDealEligibilityPolicy
    {
        public RealtorDealEligibilityPolicy(RealtorEfficiencyOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
        }

        public RealtorDealEligibilityResponse Evaluate(
            bool restrictionsEnabled,
            EligibilityTierOptions? matchedTier,
            EligibilityOptions eligibilitySettings,
            double clientTrustScore,
            double adminPerformanceScore,
            int criticalComplaintsCount,
            int completedDealsHistoryCount,
            string? realtorLevel = null)
        {
            if (!restrictionsEnabled || matchedTier is null)
            {
                return new RealtorDealEligibilityResponse(
                    IsEligible: true,
                    IsExpensiveDeal: false,
                    ClientTrustScore: null,
                    AdminPerformanceScore: null,
                    EligibilityTierName: null,
                    BlockReasonCode: null,
                    BlockReason: null);
            }

            if (!CanRealtorTakeTier(realtorLevel, matchedTier.Name))
            {
                return new RealtorDealEligibilityResponse(
                    IsEligible: false,
                    IsExpensiveDeal: true,
                    ClientTrustScore: null,
                    AdminPerformanceScore: null,
                    EligibilityTierName: matchedTier.Name,
                    BlockReasonCode: "LEVEL_PRICE_RANGE",
                    BlockReason: $"Заявка доступна с уровня {matchedTier.Name}. Текущий уровень риелтора: {realtorLevel ?? "не определен"}.");
            }

            return new RealtorDealEligibilityResponse(
                IsEligible: true,
                IsExpensiveDeal: true,
                ClientTrustScore: null,
                AdminPerformanceScore: null,
                EligibilityTierName: matchedTier.Name,
                BlockReasonCode: null,
                BlockReason: null);
        }

        private static bool CanRealtorTakeTier(string? realtorLevel, string requiredTier)
        {
            var realtorRank = GetLevelRank(realtorLevel);
            var requiredRank = GetLevelRank(requiredTier);
            return requiredRank > 0 && realtorRank >= requiredRank;
        }

        private static int GetLevelRank(string? level)
        {
            return level?.Trim() switch
            {
                "Junior" => 1,
                "Standard" => 2,
                "Top" => 3,
                _ => 0
            };
        }
    }
}
