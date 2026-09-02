using Domain.Entities;
using Domain.Primitives;

namespace Application.Interfaces
{
    public interface IComplaintRepository : IRepository<Complaint>
    {
        Task<List<Complaint>> GetOpenComplaints();
        Task<List<Complaint>> GetResolvedByRealtor(Guid realtorId, DateTime fromUtc);
        Task<int> CountByClientAndSubjectBase(Guid clientId, string subjectBase);
        Task<List<Complaint>> GetForAdmin(
            int limit,
            ComplaintStatus? status = null,
            ComplaintCategory? category = null,
            bool? dealLinked = null);

        Task<List<Complaint>> GetForRealtor(
            Guid realtorId,
            int limit);
    }
}
