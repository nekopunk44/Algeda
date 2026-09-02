namespace Application.DTOs.ChatMessage
{
    public record SendMessageRequest(
        Guid SenderId,
        Guid ReceiverId,
        string Content);
}
