using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.ChatMessage
{
    public record DialogResponse(
        List<ChatMessageResponse> Messages);
}
