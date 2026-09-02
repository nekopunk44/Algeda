using Domain.Common;
using Domain.Enums;
using Domain.Primitives;

namespace Domain.Entities
{
    public class PropertyMatchNotificationLog : BaseEntity
    {
        public Guid RequirementId { get; private set; }
        public Guid PropertyId { get; private set; }
        public PropertyMatchNotificationType NotificationType { get; private set; }
        public DateTime SentAt { get; private set; }

        public PropertyMatchNotificationLog(
            Guid requirementId,
            Guid propertyId,
            PropertyMatchNotificationType notificationType,
            DateTime? sentAt = null)
        {
            RequirementId = requirementId;
            PropertyId = propertyId;
            NotificationType = notificationType;
            SentAt = sentAt ?? DateTime.UtcNow;

            Validate();
        }

        private PropertyMatchNotificationLog()
        {
            RequirementId = Guid.Empty;
            PropertyId = Guid.Empty;
        }

        private void Validate()
        {
            if (RequirementId == Guid.Empty)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(RequirementId)));

            if (PropertyId == Guid.Empty)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(PropertyId)));

            if (SentAt == default)
                throw new DomainException(ValidationMessages.InvalidProperty(nameof(SentAt)));
        }
    }
}
