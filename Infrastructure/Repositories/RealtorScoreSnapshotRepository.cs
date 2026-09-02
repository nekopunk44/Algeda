using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class RealtorScoreSnapshotRepository : Repository<RealtorScoreSnapshot>, IRealtorScoreSnapshotRepository
    {
        private readonly AppDbContext _context;

        public RealtorScoreSnapshotRepository(AppDbContext context)
            : base(context)
        {
            _context = context;
        }

        public async Task<RealtorScoreSnapshot?> GetLatestByRealtor(Guid realtorId)
        {
            return await _context.RealtorScoreSnapshots
                .AsNoTracking()
                .Where(x => x.RealtorId == realtorId)
                .OrderByDescending(x => x.CreatedDate)
                .FirstOrDefaultAsync();
        }

        public async Task<List<RealtorScoreSnapshot>> GetHistoryByRealtor(Guid realtorId, int limit = 50)
        {
            return await _context.RealtorScoreSnapshots
                .AsNoTracking()
                .Where(x => x.RealtorId == realtorId)
                .OrderByDescending(x => x.CreatedDate)
                .Take(limit)
                .ToListAsync();
        }
    }
}
