namespace Application.DTOs.RealtorEfficiency
{
    public record RealtorDealEligibilityResponse(
        bool IsEligible,
        bool IsExpensiveDeal,
        double? ClientTrustScore,
        double? AdminPerformanceScore,
        string? EligibilityTierName,
        string? BlockReasonCode,
        string? BlockReason);
}
