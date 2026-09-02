using Domain.Common;
using Domain.Primitives;

namespace Domain.Entities
{
    public class DealDocument : BaseEntity
    {
        public Guid DealId { get; private set; }
        public string Title { get; private set; }
        public string OriginalFileName { get; private set; }
        public string ContentType { get; private set; }
        public long FileSizeBytes { get; private set; }
        public string ContentHash { get; private set; }
        public byte[] Content { get; private set; }
        public Guid? UploadedByUserId { get; private set; }
        public string UploadedByDisplayName { get; private set; }
        public string? UploadedByEmail { get; private set; }
        public bool IsDeleted { get; private set; }
        public DateTime? DeletedAtUtc { get; private set; }
        public Guid? DeletedByUserId { get; private set; }
        public string? DeletedByDisplayName { get; private set; }

        public DealDocument(
            Guid dealId,
            string title,
            string originalFileName,
            string contentType,
            byte[] content,
            string contentHash,
            Guid? uploadedByUserId,
            string uploadedByDisplayName,
            string? uploadedByEmail = null)
        {
            DealId = dealId;
            Title = NormalizeRequired(title, 200, "Название документа");
            OriginalFileName = NormalizeRequired(originalFileName, 260, "Имя файла");
            ContentType = NormalizeRequired(contentType, 120, "Тип файла");
            Content = content is { Length: > 0 }
                ? content
                : throw new DomainException("Файл документа не должен быть пустым.");
            FileSizeBytes = content.Length;
            ContentHash = NormalizeRequired(contentHash, 128, "Хеш файла");
            UploadedByUserId = uploadedByUserId;
            UploadedByDisplayName = NormalizeRequired(uploadedByDisplayName, 200, "Пользователь");
            UploadedByEmail = NormalizeOptional(uploadedByEmail, 256);
        }

        private DealDocument()
        {
            Title = null!;
            OriginalFileName = null!;
            ContentType = null!;
            ContentHash = null!;
            Content = null!;
            UploadedByDisplayName = null!;
        }

        public void MarkDeleted(Guid? userId, string displayName)
        {
            if (IsDeleted)
                return;

            IsDeleted = true;
            DeletedAtUtc = DateTime.UtcNow;
            DeletedByUserId = userId;
            DeletedByDisplayName = NormalizeRequired(displayName, 200, "Пользователь");
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
            return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
        }
    }
}
