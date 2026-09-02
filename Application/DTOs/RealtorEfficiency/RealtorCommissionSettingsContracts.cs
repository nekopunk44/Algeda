using Domain.Primitives;

namespace Application.DTOs.RealtorEfficiency
{
    public record RealtorLevelCommissionResponse(
        RealtorLevel Level,
        decimal Percent);

    public record RealtorCommissionSettingsResponse(
        IReadOnlyList<RealtorLevelCommissionResponse> Items);

    public record UpdateRealtorLevelCommissionRequest(
        RealtorLevel Level,
        decimal Percent);

    public record UpdateRealtorCommissionSettingsRequest(
        IReadOnlyList<UpdateRealtorLevelCommissionRequest> Items);
}
