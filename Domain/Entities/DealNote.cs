using Domain.Common;
using Domain.Primitives;

namespace Domain.Entities
{
    public class DealNote : BaseEntity
    {
        public Guid DealId { get; private set; }
        public Guid? AuthorRealtorId { get; private set; }
        public string Text { get; private set; }
        public DateTime? UpdatedAtUtc { get; private set; }

        public DealNote(Guid dealId, string text, Guid? authorRealtorId = null)
        {
            DealId = dealId;
            AuthorRealtorId = authorRealtorId;
            Text = NormalizeText(text);
        }

        private DealNote()
        {
            Text = null!;
        }

        public void UpdateText(string text)
        {
            Text = NormalizeText(text);
            UpdatedAtUtc = DateTime.UtcNow;
        }

        private static string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new DomainException(ValidationMessages.NotEmpty(nameof(text)));

            var normalized = text.Trim();
            if (normalized.Length > 4000)
                throw new DomainException("Текст заметки не должен превышать 4000 символов.");

            return normalized;
        }
    }
}
