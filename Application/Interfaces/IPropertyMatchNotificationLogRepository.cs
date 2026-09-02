using Domain.Entities;
using Domain.Enums;

namespace Application.Interfaces
{
    public interface IPropertyMatchNotificationLogRepository : IRepository<PropertyMatchNotificationLog>
    {
        Task<HashSet<Guid>> GetSentPropertyIds(
            Guid requirementId,
            PropertyMatchNotificationType notificationType,
            IReadOnlyCollection<Guid> propertyIds);

        Task<HashSet<Guid>> GetSentRequirementIds(
            Guid propertyId,
            PropertyMatchNotificationType notificationType,
            IReadOnlyCollection<Guid> requirementIds);

        Task<HashSet<Guid>> GetSentRequirementIds(
            Guid propertyId,
            IReadOnlyCollection<Guid> requirementIds);

        Task<List<PropertyMatchNotificationLog>> GetByRequirement(
            Guid requirementId,
            PropertyMatchNotificationType notificationType,
            int limit);

        Task<List<PropertyMatchNotificationLog>> GetByRequirement(
            Guid requirementId,
            int limit);

        Task<HashSet<Guid>> GetRequirementIdsWithNotificationType(
            IReadOnlyCollection<Guid> requirementIds,
            PropertyMatchNotificationType notificationType);

        Task AddRange(IReadOnlyCollection<PropertyMatchNotificationLog> logs);
    }
}
