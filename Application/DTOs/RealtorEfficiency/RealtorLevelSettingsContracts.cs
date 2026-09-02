using Domain.Primitives;

namespace Application.DTOs.RealtorEfficiency
{
    public record RealtorLevelRuleResponse(
        RealtorLevel Level,
        int MinCompletedDeals,
        double MinClientTrustScore,
        double MinAdminPerformanceScore,
        int SortOrder);

    public record RealtorLevelSettingsResponse(
        double DemotionBuffer,
        IReadOnlyList<RealtorLevelRuleResponse> Rules);

    public record UpdateRealtorLevelRuleRequest(
        RealtorLevel Level,
        int MinCompletedDeals,
        double MinClientTrustScore,
        double MinAdminPerformanceScore,
        int SortOrder);

    public record UpdateRealtorLevelSettingsRequest(
        double DemotionBuffer,
        IReadOnlyList<UpdateRealtorLevelRuleRequest> Rules);
}
