using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class RealtorFeedbackRepository : Repository<RealtorFeedback>, IRealtorFeedbackRepository
    {
        private readonly AppDbContext _context;

        public RealtorFeedbackRepository(AppDbContext context)
            : base(context)
        {
            _context = context;
        }

        public async Task<RealtorFeedback?> GetByDeal(Guid dealId)
        {
            return await _context.RealtorFeedbacks
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.DealId == dealId);
        }

        public async Task<List<RealtorFeedback>> GetByServiceRealtor(Guid realtorId, int limit = 100)
        {
            return await _context.RealtorFeedbacks
                .AsNoTracking()
                .Where(x => x.ServiceRealtorId == realtorId)
                .OrderByDescending(x => x.CreatedDate)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<List<RealtorFeedback>> GetByServiceRealtor(Guid realtorId, DateTime fromUtc)
        {
            return await _context.RealtorFeedbacks
                .AsNoTracking()
                .Where(x => x.ServiceRealtorId == realtorId)
                .Where(x => x.CreatedDate >= fromUtc)
                .OrderByDescending(x => x.CreatedDate)
                .ToListAsync();
        }

        public async Task<List<RealtorFeedback>> GetByPropertyResponsibleRealtor(Guid realtorId, int limit = 100)
        {
            return await _context.RealtorFeedbacks
                .AsNoTracking()
                .Where(x => x.PropertyResponsibleRealtorId == realtorId)
                .OrderByDescending(x => x.CreatedDate)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<List<RealtorFeedback>> GetByPropertyResponsibleRealtor(Guid realtorId, DateTime fromUtc)
        {
            return await _context.RealtorFeedbacks
                .AsNoTracking()
                .Where(x => x.PropertyResponsibleRealtorId == realtorId)
                .Where(x => x.CreatedDate >= fromUtc)
                .OrderByDescending(x => x.CreatedDate)
                .ToListAsync();
        }
    }
}
