using Application.DTOs.PropertyMatching;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class PropertyRepository : Repository<Property>, IPropertyRepository
    {
        private readonly AppDbContext _context;

        public PropertyRepository(AppDbContext context)
            : base(context)
        {
            _context = context;
        }

        public override async Task<Property?> GetById(Guid id)
        {
            return await QueryWithCriteria(_context.Properties.AsNoTracking())
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public override async Task<List<Property>> Get(int limit)
        {
            return await QueryWithCriteria(_context.Properties.AsNoTracking())
                .OrderByDescending(x => x.CreatedDate)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<List<Property>> GetAvailable(int limit, int offset)
        {
            return await QueryWithCriteria(_context.Properties.AsNoTracking())
                .Where(x => x.Status == PropertyStatus.Available)
                .OrderByDescending(x => x.CreatedDate)
                .ThenByDescending(x => x.Id)
                .Skip(offset)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<List<Property>> GetByResponsibleRealtor(Guid realtorId, DateTime fromUtc)
        {
            return await QueryWithCriteria(_context.Properties.AsNoTracking())
                .Where(x => x.ResponsibleRealtorId == realtorId)
                .Where(x => x.CreatedDate >= fromUtc)
                .OrderByDescending(x => x.CreatedDate)
                .ToListAsync();
        }

        public async Task<List<Property>> GetByIds(IReadOnlyCollection<Guid> ids)
        {
            if (ids.Count == 0)
            {
                return [];
            }

            return await QueryWithCriteria(_context.Properties.AsNoTracking())
                .Where(x => ids.Contains(x.Id))
                .ToListAsync();
        }

        public async Task<Property?> GetByIdForUpdateWithCriteria(Guid id)
        {
            return await QueryWithCriteria(_context.Properties.AsTracking())
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public void AddCriterionValue(PropertyCriterionValue value)
        {
            _context.PropertyCriterionValues.Add(value);
        }

        public async Task<List<PropertyMatchCandidate>> GetCandidatesForRequirement(
            ClientRequirement requirement,
            int take)
        {
            if (take <= 0)
                return [];

            var desiredTypes = requirement.DesiredTypes
                .Where(x => x != PropertyType.Undefined)
                .Distinct()
                .Select(x => (int)x)
                .ToArray();

            if (desiredTypes.Length == 0 && requirement.DesiredType != PropertyType.Undefined)
            {
                desiredTypes = [(int)requirement.DesiredType];
            }

            var hasDesiredTypesFilter = desiredTypes.Length > 0;
            var hasMaxAreaFilter = requirement.MaxArea.HasValue;
            var maxArea = requirement.MaxArea ?? 0d;
            var hasAddressFilter = !string.IsNullOrWhiteSpace(requirement.AddressQuery);
            var addressPattern = hasAddressFilter
                ? $"%{requirement.AddressQuery!.Trim()}%"
                : string.Empty;

            var candidates = await _context.Database
                .SqlQuery<PropertyCandidateSqlRow>($@"
                    WITH target AS (
                        SELECT ST_SetSRID(
                            ST_MakePoint({requirement.TargetLocation.Longitude}, {requirement.TargetLocation.Latitude}),
                            4326)::geography AS point
                    )
                    SELECT p.""Id"" AS ""PropertyId"",
                           ST_Distance(p.""Location"", target.point) AS ""DistanceMeters""
                    FROM ""Properties"" AS p
                    CROSS JOIN target
                    WHERE p.""Status"" = {(int)PropertyStatus.Available}
                      AND ({hasDesiredTypesFilter} = FALSE OR p.""Type"" = ANY({desiredTypes}))
                      AND p.""Price"" >= {requirement.MinPrice}
                      AND p.""Price"" <= {requirement.MaxPrice}
                      AND p.""Area"" >= {requirement.MinArea}
                      AND ({hasMaxAreaFilter} = FALSE OR p.""Area"" <= {maxArea})
                      AND ({hasAddressFilter} = FALSE OR p.""Address"" ILIKE {addressPattern})
                      AND ({requirement.IgnoreArea} = TRUE OR ST_DWithin(p.""Location"", target.point, {requirement.SearchRadiusMeters}))
                    ORDER BY p.""Location"" <-> target.point,
                             ABS(p.""Price"" - {requirement.MinPrice}),
                             p.""CreatedDate"" DESC
                    LIMIT {take}")
                .ToListAsync();

            if (candidates.Count == 0)
                return [];

            var candidateIds = candidates.Select(x => x.PropertyId).ToList();
            var propertiesById = await QueryWithCriteria(_context.Properties.AsNoTracking())
                .Where(x => candidateIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id);

            var orderedCandidates = new List<PropertyMatchCandidate>(candidates.Count);
            foreach (var candidate in candidates)
            {
                if (propertiesById.TryGetValue(candidate.PropertyId, out var property))
                    orderedCandidates.Add(new PropertyMatchCandidate(property, candidate.DistanceMeters));
            }

            return orderedCandidates;
        }

        private static IQueryable<Property> QueryWithCriteria(IQueryable<Property> query)
        {
            return query
                .Include(x => x.CriterionValues)
                    .ThenInclude(x => x.CriterionDefinition!)
                        .ThenInclude(x => x.Options)
                .AsSplitQuery();
        }

        private sealed class PropertyCandidateSqlRow
        {
            public Guid PropertyId { get; set; }
            public double DistanceMeters { get; set; }
        }
    }
}
