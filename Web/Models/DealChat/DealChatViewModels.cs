using Web.Models.Api;

namespace Web.Models.DealChat;

public sealed class DealChatPageViewModel
{
    public string? SuccessMessage { get; init; }

    public ApiErrorViewModel? ApiError { get; init; }

    public Guid DealId { get; init; }

    public string? ReturnUrl { get; init; }

    public DealChatDialogViewModel? Dialog { get; init; }

    public bool CanSend => Dialog?.CanWrite == true;
}

public sealed class DealChatDialogViewModel
{
    public Guid DealId { get; init; }

    public string? PropertyTitle { get; init; }

    public Guid ClientId { get; init; }

    public Guid RealtorId { get; init; }

    public Guid CurrentActorId { get; init; }

    public string? ClientName { get; init; }

    public string? RealtorName { get; init; }

    public bool CanWrite { get; init; }

    public string? BlockReason { get; init; }

    public IReadOnlyList<DealChatMessageViewModel> Messages { get; init; } = [];
}

public sealed class DealChatMessageViewModel
{
    public Guid Id { get; init; }

    public Guid DealId { get; init; }

    public Guid SenderId { get; init; }

    public Guid ReceiverId { get; init; }

    public string Content { get; init; } = string.Empty;

    public bool IsRead { get; init; }

    public DateTime CreatedDate { get; init; }

    public bool IsOutgoing { get; init; }
}

public sealed class DealChatSummaryViewModel
{
    public Guid DealId { get; init; }

    public string? PropertyTitle { get; init; }

    public Guid ClientId { get; init; }

    public string? ClientName { get; init; }

    public string? ClientPhoneNumber { get; init; }

    public string? ClientEmail { get; init; }

    public Guid RealtorId { get; init; }

    public string? RealtorName { get; init; }

    public string? RealtorPhoneNumber { get; init; }

    public string? RealtorEmail { get; init; }

    public string DealStatus { get; init; } = "Undefined";

    public DateTime? LastMessageAtUtc { get; init; }

    public string? LastMessagePreview { get; init; }

    public int MessageCount { get; init; }
}

public sealed class DealChatNotificationViewModel
{
    public Guid DealId { get; init; }

    public string? CounterpartyName { get; init; }

    public string Preview { get; init; } = string.Empty;

    public int UnreadCount { get; init; }

    public DateTime LastMessageAtUtc { get; init; }
}
