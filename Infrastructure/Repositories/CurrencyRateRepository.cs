using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class CurrencyRateRepository : Repository<CurrencyRate>, ICurrencyRateRepository
    {
        private readonly AppDbContext _context;

        public CurrencyRateRepository(AppDbContext context)
            : base(context)
        {
            _context = context;
        }

        public override async Task<List<CurrencyRate>> Get(int limit)
        {
            return await Get(limit, includeInactive: false);
        }

        public async Task<List<CurrencyRate>> Get(int limit, bool includeInactive)
        {
            var query = _context.CurrencyRates.AsNoTracking();

            if (!includeInactive)
            {
                query = query.Where(x => x.IsActive);
            }

            return await query
                .OrderBy(x => x.Code)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<CurrencyRate?> GetByCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;

            var normalized = code.Trim().ToUpperInvariant();

            return await _context.CurrencyRates
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Code == normalized);
        }

        public async Task<CurrencyRate?> GetByIdForUpdate(Guid id)
        {
            return await _context.CurrencyRates
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
