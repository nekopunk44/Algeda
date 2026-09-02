using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class ClientRequirementRepository : Repository<ClientRequirement>, IClientRequirementRepository
    {
        private readonly AppDbContext _context;

        public ClientRequirementRepository(AppDbContext context)
            : base(context)
        {
            _context = context;
        }

        public override async Task<ClientRequirement?> GetById(Guid id)
        {
            return await QueryWithCriteria(_context.ClientRequirements)
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public override async Task<List<ClientRequirement>> Get(int limit)
        {
            return await QueryWithCriteria(_context.ClientRequirements.AsNoTracking())
                .OrderByDescending(x => x.CreatedDate)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<ClientRequirement?> GetActiveByClient(Guid clientId)
        {
            return await QueryWithCriteria(_context.ClientRequirements.AsNoTracking())
                .FirstOrDefaultAsync(x => x.ClientId == clientId && x.IsActive);
        }

        public async Task<List<ClientRequirement>> GetActive(int limit)
        {
            return await QueryWithCriteria(_context.ClientRequirements.AsNoTracking())
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.CreatedDate)
                .Take(limit)
                .ToListAsync();
        }

        private static IQueryable<ClientRequirement> QueryWithCriteria(IQueryable<ClientRequirement> query)
        {
            return query
                .Include(x => x.Criteria);
        }

        public override void Update(ClientRequirement entity)
        {
            // By calling DetectChanges, we ensure EF has tracked all the items that were added to criteria.
            _context.ChangeTracker.DetectChanges();

            // If the user replaces criteria, new objects with Guids are added.
            // EF tries to mark them as 'Modified' because they have non-default primary keys.
            // This causes DbUpdateConcurrencyException. We must manually set their state back to 'Added'.
            foreach (var criterion in entity.Criteria)
            {
                var entry = _context.Entry(criterion);
                if (entry.State == EntityState.Modified)
                {
                    entry.State = EntityState.Added;
                }
            }

            base.Update(entity);
        }
    }
}
