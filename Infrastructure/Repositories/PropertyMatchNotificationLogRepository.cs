using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class PropertyMatchNotificationLogRepository : Repository<PropertyMatchNotificationLog>, IPropertyMatchNotificationLogRepository
    {
        private readonly AppDbContext _context;

        public PropertyMatchNotificationLogRepository(AppDbContext context)
            : base(context)
        {
            _context = context;
        }

        public async Task<HashSet<Guid>> GetSentPropertyIds(
            Guid requirementId,
            PropertyMatchNotificationType notificationType,
            IReadOnlyCollection<Guid> propertyIds)
        {
            if (propertyIds.Count == 0)
                return [];

            return await _context.PropertyMatchNotificationLogs
                .AsNoTracking()
                .Where(x => x.RequirementId == requirementId
                            && x.NotificationType == notificationType
                            && propertyIds.Contains(x.PropertyId))
                .Select(x => x.PropertyId)
                .ToHashSetAsync();
        }

        public async Task<HashSet<Guid>> GetSentRequirementIds(
            Guid propertyId,
            PropertyMatchNotificationType notificationType,
            IReadOnlyCollection<Guid> requirementIds)
        {
            if (requirementIds.Count == 0)
                return [];

            return await _context.PropertyMatchNotificationLogs
                .AsNoTracking()
                .Where(x => x.PropertyId == propertyId
                            && x.NotificationType == notificationType
                            && requirementIds.Contains(x.RequirementId))
                .Select(x => x.RequirementId)
                .ToHashSetAsync();
        }

        public async Task<HashSet<Guid>> GetSentRequirementIds(
            Guid propertyId,
            IReadOnlyCollection<Guid> requirementIds)
        {
            if (requirementIds.Count == 0)
                return [];

            return await _context.PropertyMatchNotificationLogs
                .AsNoTracking()
                .Where(x => x.PropertyId == propertyId
                            && requirementIds.Contains(x.RequirementId))
                .Select(x => x.RequirementId)
                .ToHashSetAsync();
        }

        public async Task<List<PropertyMatchNotificationLog>> GetByRequirement(
            Guid requirementId,
            PropertyMatchNotificationType notificationType,
            int limit)
        {
            var safeLimit = Math.Clamp(limit, 1, 100);

            return await _context.PropertyMatchNotificationLogs
                .AsNoTracking()
                .Where(x => x.RequirementId == requirementId
                            && x.NotificationType == notificationType)
                .OrderByDescending(x => x.SentAt)
                .Take(safeLimit)
                .ToListAsync();
        }

        public async Task<List<PropertyMatchNotificationLog>> GetByRequirement(
            Guid requirementId,
            int limit)
        {
            var safeLimit = Math.Clamp(limit, 1, 100);

            return await _context.PropertyMatchNotificationLogs
                .AsNoTracking()
                .Where(x => x.RequirementId == requirementId)
                .OrderByDescending(x => x.SentAt)
                .Take(safeLimit)
                .ToListAsync();
        }

        public async Task<HashSet<Guid>> GetRequirementIdsWithNotificationType(
            IReadOnlyCollection<Guid> requirementIds,
            PropertyMatchNotificationType notificationType)
        {
            if (requirementIds.Count == 0)
            {
                return [];
            }

            return await _context.PropertyMatchNotificationLogs
                .AsNoTracking()
                .Where(x => x.NotificationType == notificationType
                            && requirementIds.Contains(x.RequirementId))
                .Select(x => x.RequirementId)
                .Distinct()
                .ToHashSetAsync();
        }

        public async Task AddRange(IReadOnlyCollection<PropertyMatchNotificationLog> logs)
        {
            if (logs.Count == 0)
                return;

            await _context.PropertyMatchNotificationLogs.AddRangeAsync(logs);
            await _context.SaveChangesAsync();
        }
    }
}
