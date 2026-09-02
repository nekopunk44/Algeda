using Application.DTOs.RealtorEfficiency;

namespace Application.Interfaces
{
    public interface IRealtorEfficiencyCalculationService
    {
        Task<RealtorScoreBreakdownResponse> RecalculateAndSave(
            Guid realtorId,
            int? days = null,
            string? calculationVersion = null);

        Task<RealtorScoreBreakdownResponse?> GetLatestBreakdown(Guid realtorId);
        Task<List<RealtorScoreBreakdownResponse>> GetHistory(Guid realtorId, int limit = 20);
    }
}
