using Domain.Common;
using Domain.Primitives;

namespace Domain.Entities
{
    public class DealDocumentAccessLog : BaseEntity
    {
        public Guid DealId { get; private set; }
        public Guid DealDocumentId { get; private set; }
        public string DocumentTitle { get; private set; }
        public string DocumentFileName { get; private set; }
        public DealDocumentAction Action { get; private set; }
        public Guid? ActorUserId { get; private set; }
        public string ActorDisplayName { get; private set; }
        public string? ActorEmail { get; private set; }
        public string ActorRole { get; private set; }
        public string? IpAddress { get; private set; }
        public string? UserAgent { get; private set; }

        public DealDocumentAccessLog(
            Guid dealId,
            Guid dealDocumentId,
            string documentTitle,
            string documentFileName,
            DealDocumentAction action,
            Guid? actorUserId,
            string actorDisplayName,
            string? actorEmail,
            string actorRole,
            string? ipAddress,
            string? userAgent)
        {
            DealId = dealId;
            DealDocumentId = dealDocumentId;
            DocumentTitle = NormalizeRequired(documentTitle, 200, "Название документа");
            DocumentFileName = NormalizeRequired(documentFileName, 260, "Имя файла");
            Action = action;
            ActorUserId = actorUserId;
            ActorDisplayName = NormalizeRequired(actorDisplayName, 200, "Пользователь");
            ActorEmail = NormalizeOptional(actorEmail, 256);
            ActorRole = NormalizeRequired(actorRole, 80, "Роль");
            IpAddress = NormalizeOptional(ipAddress, 80);
            UserAgent = NormalizeOptional(userAgent, 500);
        }

        private DealDocumentAccessLog()
        {
            DocumentTitle = null!;
            DocumentFileName = null!;
            ActorDisplayName = null!;
            ActorRole = null!;
        }

        private static string NormalizeRequired(string value, int maxLength, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new DomainException($"{fieldName} не должно быть пустым.");

            var normalized = value.Trim();
            if (normalized.Length > maxLength)
                throw new DomainException($"{fieldName} не должно превышать {maxLength} символов.");

            return normalized;
        }

        private static string? NormalizeOptional(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var normalized = value.Trim();
            return normalized.Length <= maxLength
                ? normalized
                : normalized[..maxLength];
        }
    }
}
