using Domain.Common;
using Domain.Entities;
using Domain.Primitives;

namespace Application.Interfaces
{
    public interface IRealtorActivityLogRepository : IRepository<RealtorActivityLog>
    {
        Task<List<RealtorActivityLog>> GetByRealtor(Guid realtorId);
        Task<List<RealtorActivityLog>> GetByRealtor(Guid realtorId, DateTime fromUtc);
    }
}
