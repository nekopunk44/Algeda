using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class RealtorRepository : Repository<Realtor>, IRealtorRepository
    {
        private readonly AppDbContext _context;

        public RealtorRepository(AppDbContext context)
            : base(context)
        {
            _context = context;
        }

        public async Task<Realtor?> GetByPhone(string phone)
        {
            return await _context.Realtors
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.PhoneNumber == phone);
        }

        public async Task<List<Realtor>> GetTopRealtors(int count)
        {
            return await _context.Realtors
                .AsNoTracking()
                .OrderByDescending(x => x.CurrentKpiScore)
                .ThenByDescending(x => x.AverageRating)
                .Take(count)
                .ToListAsync();
        }

        public async Task<List<Realtor>> GetActive()
        {
            return await _context.Realtors
                .AsNoTracking()
                .OrderByDescending(x => x.CurrentKpiScore)
                .ToListAsync();
        }
    }
}
