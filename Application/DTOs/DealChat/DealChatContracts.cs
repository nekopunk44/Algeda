namespace Application.DTOs.DealChat;

public sealed record SendDealChatMessageRequest(string Content);

public sealed record DealChatMessageResponse(
    Guid Id,
    Guid DealId,
    Guid SenderId,
    Guid ReceiverId,
    string Content,
    bool IsRead,
    DateTime CreatedDate,
    bool IsOutgoing);

public sealed record DealChatDialogResponse(
    Guid DealId,
    Guid ClientId,
    Guid RealtorId,
    Guid CurrentActorId,
    string? ClientName,
    string? RealtorName,
    bool CanWrite,
    string? BlockReason,
    IReadOnlyList<DealChatMessageResponse> Messages);

public sealed record DealChatSummaryResponse(
    Guid DealId,
    Guid ClientId,
    string? ClientName,
    Guid RealtorId,
    string? RealtorName,
    string DealStatus,
    DateTime? LastMessageAtUtc,
    string? LastMessagePreview,
    int MessageCount);

public sealed record DealChatNotificationResponse(
    Guid DealId,
    string? CounterpartyName,
    string Preview,
    int UnreadCount,
    DateTime LastMessageAtUtc);
