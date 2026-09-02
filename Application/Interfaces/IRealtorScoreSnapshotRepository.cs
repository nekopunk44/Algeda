using Domain.Entities;

namespace Application.Interfaces
{
    public interface IRealtorScoreSnapshotRepository : IRepository<RealtorScoreSnapshot>
    {
        Task<RealtorScoreSnapshot?> GetLatestByRealtor(Guid realtorId);
        Task<List<RealtorScoreSnapshot>> GetHistoryByRealtor(Guid realtorId, int limit = 50);
    }
}
