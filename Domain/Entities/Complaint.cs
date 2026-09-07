using Domain.Common;
using Domain.Primitives;

namespace Domain.Entities
{
    /// <summary>
    /// Жалоба на риелтора от клиента
    /// </summary>
    public class Complaint : BaseEntity
    {
        public Guid ClientId { get; private set; }
        public Guid? TargetRealtorId { get; private set; } // На кого жалуемся (если применимо)
        public Guid? DealId { get; private set; } // Если жалоба связана с конкретной сделкой
        public Guid? PropertyId { get; private set; } // Если жалоба связана с карточкой объекта
        public ComplaintCategory Category { get; private set; }
        public string Subject { get; private set; } // Тема жалобы
        public string Description { get; private set; } // Подробности
        public ComplaintStatus Status { get; private set; }
        public DateTime? ResolvedAt { get; private set; }
        public string? AdminResolution { get; private set; } // Решение администратора
        public ComplaintReviewVerdict ModerationVerdict { get; private set; }

        public Complaint(Guid clientId, Guid targetRealtorId, string subject, string description)
            : this(
                clientId,
                targetRealtorId,
                null,
                null,
                ComplaintCategory.Realtor,
                subject,
                description)
        {
        }

        public Complaint(
            Guid clientId,
            Guid? targetRealtorId,
            Guid? dealId,
            Guid? propertyId,
            ComplaintCategory category,
            string subject,
            string description)
        {
            ClientId = clientId;
            TargetRealtorId = targetRealtorId;
            DealId = dealId;
            PropertyId = propertyId;
            Category = category;
            Subject = subject;
            Description = description;
            Status = ComplaintStatus.Opened;
            ModerationVerdict = ComplaintReviewVerdict.Undefined;

            Validate();
        }

        // Администратор берет в работу
        public void MarkAsInProgress()
        {
            if (Status == ComplaintStatus.Resolved)
                throw new DomainException(ValidationMessages.CannotChangeState("Жалоба"));

            Status = ComplaintStatus.InProgress;
        }

        public void MarkAsOpened()
        {
            if (Status == ComplaintStatus.Resolved)
                throw new DomainException(ValidationMessages.CannotChangeState("Жалоба"));

            Status = ComplaintStatus.Opened;
        }

        // Администратор закрывает жалобу с вердиктом
        public void Resolve(ComplaintReviewVerdict verdict, string resolution)
        {
            if (Status == ComplaintStatus.Resolved)
                throw new DomainException(ValidationMessages.AlreadyInState("Жалоба", "Решена"));

            if (verdict == ComplaintReviewVerdict.Undefined)
                throw new DomainException("Не выбран вердикт по жалобе.");

            if (string.IsNullOrWhiteSpace(resolution))
                throw new DomainException(ValidationMessages.NotEmpty(nameof(resolution)));

            Status = ComplaintStatus.Resolved;
            ModerationVerdict = verdict;
            AdminResolution = resolution;
            ResolvedAt = DateTime.UtcNow;
        }

        private void Validate()
        {
            if (ClientId == Guid.Empty)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(ClientId)));

            if (Category == ComplaintCategory.Undefined)
                throw new DomainException("Не указана категория жалобы.");

            if (Category == ComplaintCategory.Realtor && !TargetRealtorId.HasValue)
                throw new DomainException("Для жалобы на риелтора нужно указать целевого риелтора.");

            if (TargetRealtorId.HasValue && TargetRealtorId.Value == Guid.Empty)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(TargetRealtorId)));

            if (DealId.HasValue && DealId.Value == Guid.Empty)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(DealId)));

            if (PropertyId.HasValue && PropertyId.Value == Guid.Empty)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(PropertyId)));

            if (string.IsNullOrWhiteSpace(Subject))
                throw new DomainException(ValidationMessages.NotEmpty(nameof(Subject)));

            if (string.IsNullOrWhiteSpace(Description))
                throw new DomainException(ValidationMessages.NotEmpty(nameof(Description)));
        }
    }
}
