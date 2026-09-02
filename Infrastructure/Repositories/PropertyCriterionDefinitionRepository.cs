using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class PropertyCriterionDefinitionRepository
        : Repository<PropertyCriterionDefinition>, IPropertyCriterionDefinitionRepository
    {
        private readonly AppDbContext _context;

        public PropertyCriterionDefinitionRepository(AppDbContext context)
            : base(context)
        {
            _context = context;
        }

        public override async Task<PropertyCriterionDefinition?> GetById(Guid id)
        {
            return await QueryWithOptions(_context.PropertyCriterionDefinitions.AsNoTracking())
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public override async Task<List<PropertyCriterionDefinition>> Get(int limit)
        {
            return await QueryWithOptions(_context.PropertyCriterionDefinitions.AsNoTracking())
                .OrderBy(x => x.Category)
                .ThenBy(x => x.DisplayName)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<List<PropertyCriterionDefinition>> Get(int limit, bool includeHidden)
        {
            var query = QueryWithOptions(_context.PropertyCriterionDefinitions.AsNoTracking());

            if (!includeHidden)
            {
                query = query.Where(x => !x.IsHidden);
            }

            return await query
                .OrderBy(x => x.Category)
                .ThenBy(x => x.DisplayName)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<(List<PropertyCriterionDefinition> Items, int TotalCount)> GetPage(
            int page,
            int pageSize,
            bool includeHidden,
            string? search,
            string? sort)
        {
            var safePage = Math.Max(page, 1);
            var safePageSize = Math.Clamp(pageSize, 1, 100);

            IQueryable<PropertyCriterionDefinition> query =
                _context.PropertyCriterionDefinitions.AsNoTracking();

            if (!includeHidden)
            {
                query = query.Where(x => !x.IsHidden);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var needle = search.Trim().ToLowerInvariant();
                query = query.Where(x =>
                    x.Code.ToLower().Contains(needle)
                    || x.DisplayName.ToLower().Contains(needle)
                    || (x.Category != null && x.Category.ToLower().Contains(needle)));
            }

            query = sort?.Trim().ToLowerInvariant() switch
            {
                "name_desc" => query.OrderByDescending(x => x.DisplayName).ThenBy(x => x.Code),
                "created_asc" => query.OrderBy(x => x.CreatedDate),
                "created_desc" => query.OrderByDescending(x => x.CreatedDate),
                "code_asc" => query.OrderBy(x => x.Code),
                "code_desc" => query.OrderByDescending(x => x.Code),
                _ => query.OrderBy(x => x.DisplayName).ThenBy(x => x.Code)
            };

            var totalCount = await query.CountAsync();
            var pageIds = await query
                .Skip((safePage - 1) * safePageSize)
                .Take(safePageSize)
                .Select(x => x.Id)
                .ToListAsync();

            if (pageIds.Count == 0)
            {
                return ([], totalCount);
            }

            var itemsById = await QueryWithOptions(_context.PropertyCriterionDefinitions.AsNoTracking())
                .Where(x => pageIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id);

            var items = pageIds
                .Where(itemsById.ContainsKey)
                .Select(id => itemsById[id])
                .ToList();

            return (items, totalCount);
        }

        public async Task<PropertyCriterionDefinition?> GetByIdForUpdate(Guid id)
        {
            return await QueryWithOptions(_context.PropertyCriterionDefinitions)
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<PropertyCriterionDefinition?> GetByCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;

            var normalizedCode = code.Trim().ToLowerInvariant();

            return await QueryWithOptions(_context.PropertyCriterionDefinitions.AsNoTracking())
                .FirstOrDefaultAsync(x => x.Code == normalizedCode);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        private static IQueryable<PropertyCriterionDefinition> QueryWithOptions(
            IQueryable<PropertyCriterionDefinition> query)
        {
            return query.Include(x => x.Options);
        }
    }
}
