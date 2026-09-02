using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.ChatMessage
{
    public record ChatMessageResponse(
        Guid Id,
        Guid SenderId,
        Guid ReceiverId,
        string Content,
        bool IsRead,
        DateTime CreatedDate);
}
