using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class RealtorActivityLogRepository : Repository<RealtorActivityLog>, IRealtorActivityLogRepository
    {
        private readonly AppDbContext _context;

        public RealtorActivityLogRepository(AppDbContext context)
            : base(context)
        {
            _context = context;
        }

        public async Task<List<RealtorActivityLog>> GetByRealtor(Guid realtorId)
        {
            return await _context.RealtorActivityLogs
                .AsNoTracking()
                .Where(x => x.RealtorId == realtorId)
                .OrderByDescending(x => x.CreatedDate)
                .ToListAsync();
        }

        public async Task<List<RealtorActivityLog>> GetByRealtor(Guid realtorId, DateTime fromUtc)
        {
            return await _context.RealtorActivityLogs
                .AsNoTracking()
                .Where(x => x.RealtorId == realtorId)
                .Where(x => x.CreatedDate >= fromUtc)
                .ToListAsync();
        }
    }
}
