using Application.DTOs.RealtorEfficiency;

namespace Application.Interfaces
{
    public interface IRealtorFeedbackService
    {
        Task<RealtorFeedbackResponse> SubmitForCurrentClient(
            string clientEmail,
            SubmitDealFeedbackRequest request);

        Task<DealFeedbackStateResponse> GetStateForCurrentClient(string clientEmail, Guid dealId);

        Task<RealtorFeedbackSummaryResponse> GetSummaryForRealtor(Guid realtorId, int limit);
    }
}
