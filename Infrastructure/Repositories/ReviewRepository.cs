using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class ReviewRepository : Repository<Review>, IReviewRepository
    {
        private readonly AppDbContext _context;

        public ReviewRepository(AppDbContext context)
            : base(context)
        {
            _context = context;
        }

        public async Task<Review?> GetByDeal(Guid dealId)
        {
            return await _context.Reviews
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.DealId == dealId);
        }

        public async Task<List<Review>> GetByRealtor(Guid realtorId)
        {
            return await _context.Reviews
                .AsNoTracking()
                .Where(x => x.RealtorId == realtorId)
                .OrderByDescending(x => x.CreatedDate)
                .ToListAsync();
        }

        public async Task<List<Review>> GetByRealtor(Guid realtorId, DateTime fromUtc)
        {
            return await _context.Reviews
                .AsNoTracking()
                .Where(x => x.RealtorId == realtorId)
                .Where(x => x.CreatedDate >= fromUtc)
                .ToListAsync();
        }
    }
}
