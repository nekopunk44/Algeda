namespace Application.DTOs.RealtorEfficiency
{
    public record RealtorEligibilityTierResponse(
        string Name,
        decimal MinPrice,
        decimal? MaxPrice,
        double MinClientTrustScore,
        double MinAdminPerformanceScore,
        int SortOrder);

    public record RealtorEligibilitySettingsResponse(
        bool RestrictionsEnabled,
        int MinConfirmedHistoryDeals,
        int CriticalComplaintLookbackDays,
        int ScoreSnapshotMaxAgeHours,
        bool BlockOnCriticalComplaints,
        IReadOnlyList<RealtorEligibilityTierResponse> PriceTiers);

    public record UpdateRealtorEligibilityTierRequest(
        string Name,
        decimal MinPrice,
        decimal? MaxPrice,
        double MinClientTrustScore,
        double MinAdminPerformanceScore,
        int SortOrder);

    public record UpdateRealtorEligibilitySettingsRequest(
        bool RestrictionsEnabled,
        int MinConfirmedHistoryDeals,
        int CriticalComplaintLookbackDays,
        int ScoreSnapshotMaxAgeHours,
        bool BlockOnCriticalComplaints,
        IReadOnlyList<UpdateRealtorEligibilityTierRequest> PriceTiers);
}