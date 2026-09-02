using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class DealRepository : Repository<Deal>, IDealRepository
    {
        private readonly AppDbContext _context;

        public DealRepository(AppDbContext context)
            : base(context)
        {
            _context = context;
        }

        public override async Task<Deal?> GetById(Guid id)
        {
            return await QueryWithNotes(_context.Deals.AsNoTracking())
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<Deal?> GetByIdWithNotes(Guid id)
        {
            return await QueryWithNotes(_context.Deals)
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<List<Deal>> GetIncoming(int limit)
        {
            return await QueryWithNotes(_context.Deals.AsNoTracking())
                .Where(x => x.Status == DealStatus.Created && x.RealtorId == Guid.Empty)
                .OrderByDescending(x => x.CreatedDate)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<List<Deal>> GetIncomingOrAssignedToRealtor(Guid realtorId, int limit)
        {
            return await QueryWithNotes(_context.Deals.AsNoTracking())
                .Where(x =>
                    (x.Status == DealStatus.Created && x.RealtorId == Guid.Empty)
                    || x.RealtorId == realtorId)
                .OrderByDescending(x => x.CreatedDate)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<List<Deal>> GetByRealtor(Guid realtorId)
        {
            return await QueryWithNotes(_context.Deals.AsNoTracking())
                .Where(x => x.RealtorId == realtorId)
                .OrderByDescending(x => x.CreatedDate)
                .ToListAsync();
        }

        public async Task<List<Deal>> GetByClient(Guid clientId)
        {
            return await QueryWithNotes(_context.Deals.AsNoTracking())
                .Where(x => x.ClientId == clientId)
                .OrderByDescending(x => x.CreatedDate)
                .ToListAsync();
        }

        public async Task<List<Deal>> GetByProperty(Guid propertyId)
        {
            return await QueryWithNotes(_context.Deals.AsNoTracking())
                .Where(x => x.PropertyId == propertyId)
                .OrderByDescending(x => x.CreatedDate)
                .ToListAsync();
        }

        public async Task<Deal?> GetLatestSaleRequestByProperty(Guid propertyId)
        {
            return await QueryWithNotes(_context.Deals.AsNoTracking())
                .Where(x => x.PropertyId == propertyId)
                .Where(x => x.Source == DealSource.Sale)
                .OrderByDescending(x => x.CreatedDate)
                .FirstOrDefaultAsync();
        }

        public async Task<List<Deal>> GetCompletedByRealtor(Guid realtorId, DateTime fromUtc)
        {
            return await _context.Deals
                .AsNoTracking()
                .Where(x => x.RealtorId == realtorId)
                .Where(x => x.Status == DealStatus.Completed)
                .Where(x => x.CompletedAt != null && x.CompletedAt >= fromUtc)
                .ToListAsync();
        }

        public async Task<DealNote?> AddNote(Guid dealId, string text, Guid? authorRealtorId)
        {
            var exists = await _context.Deals
                .AsNoTracking()
                .AnyAsync(x => x.Id == dealId);

            if (!exists)
            {
                return null;
            }

            var note = new DealNote(dealId, text, authorRealtorId);
            await _context.DealNotes.AddAsync(note);
            await _context.SaveChangesAsync();
            return note;
        }

        public async Task<DealNote?> UpdateNote(Guid dealId, Guid noteId, string text)
        {
            var note = await _context.DealNotes
                .FirstOrDefaultAsync(x => x.DealId == dealId && x.Id == noteId);

            if (note is null)
            {
                return null;
            }

            note.UpdateText(text);
            await _context.SaveChangesAsync();
            return note;
        }

        public async Task<bool> DeleteNote(Guid dealId, Guid noteId)
        {
            var deleted = await _context.DealNotes
                .Where(x => x.DealId == dealId && x.Id == noteId)
                .ExecuteDeleteAsync();

            return deleted > 0;
        }

        private static IQueryable<Deal> QueryWithNotes(IQueryable<Deal> query)
        {
            return query
                .Include(x => x.Notes);
        }
    }
}
