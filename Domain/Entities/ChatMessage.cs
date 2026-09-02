using Domain.Common;
using Domain.Primitives;

namespace Domain.Entities
{
    public class ChatMessage : BaseEntity
    {
        public Guid? DealId { get; private set; }
        public Guid SenderId { get; private set; }
        public Guid ReceiverId { get; private set; }
        public string Content { get; private set; }
        public bool IsRead { get; private set; }

        public ChatMessage(Guid senderId, Guid receiverId, string content)
            : this(senderId, receiverId, content, null)
        {
        }

        public ChatMessage(Guid senderId, Guid receiverId, string content, Guid? dealId)
        {
            DealId = dealId;
            SenderId = senderId;
            ReceiverId = receiverId;
            Content = content;
            IsRead = false;

            Validate();
        }

        // Метод для пометки сообщения прочитанным
        public void MarkAsRead()
        {
            IsRead = true;
        }

        private void Validate()
        {
            if (SenderId == Guid.Empty)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(SenderId)));

            if (ReceiverId == Guid.Empty)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(ReceiverId)));

            if (DealId.HasValue && DealId.Value == Guid.Empty)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(DealId)));

            if (string.IsNullOrWhiteSpace(Content))
                throw new DomainException(ValidationMessages.NotEmpty(nameof(Content)));
        }
    }
}
