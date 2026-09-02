using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public sealed class DealDocumentRepository : Repository<DealDocument>, IDealDocumentRepository
    {
        private readonly AppDbContext _context;

        public DealDocumentRepository(AppDbContext context)
            : base(context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<DealDocument>> GetActiveByDeal(
            Guid dealId,
            CancellationToken cancellationToken = default)
        {
            return await _context.DealDocuments
                .AsNoTracking()
                .Where(x => x.DealId == dealId && !x.IsDeleted)
                .OrderByDescending(x => x.CreatedDate)
                .ToListAsync(cancellationToken);
        }

        public Task<DealDocument?> GetActiveById(
            Guid dealId,
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            return _context.DealDocuments
                .FirstOrDefaultAsync(x => x.DealId == dealId && x.Id == documentId && !x.IsDeleted, cancellationToken);
        }
    }
}
