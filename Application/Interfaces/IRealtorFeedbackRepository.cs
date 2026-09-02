using Domain.Entities;

namespace Application.Interfaces
{
    public interface IRealtorFeedbackRepository : IRepository<RealtorFeedback>
    {
        Task<RealtorFeedback?> GetByDeal(Guid dealId);
        Task<List<RealtorFeedback>> GetByServiceRealtor(Guid realtorId, int limit = 100);
        Task<List<RealtorFeedback>> GetByPropertyResponsibleRealtor(Guid realtorId, int limit = 100);
        Task<List<RealtorFeedback>> GetByServiceRealtor(Guid realtorId, DateTime fromUtc);
        Task<List<RealtorFeedback>> GetByPropertyResponsibleRealtor(Guid realtorId, DateTime fromUtc);
    }
}
