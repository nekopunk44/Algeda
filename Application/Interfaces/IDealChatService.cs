using Application.DTOs.DealChat;

namespace Application.Interfaces;

public interface IDealChatService
{
    Task<DealChatDialogResponse> GetDialogForCurrentUser(
        Guid dealId,
        string email,
        bool isAdmin,
        bool isClient,
        bool isRealtor,
        int limit);

    Task<DealChatMessageResponse> SendMessageForCurrentUser(
        Guid dealId,
        string email,
        bool isAdmin,
        bool isClient,
        bool isRealtor,
        string content);

    Task<IReadOnlyList<DealChatSummaryResponse>> GetAdminChats(
        Guid? dealId,
        Guid? clientId,
        Guid? realtorId,
        int limit);

    Task<IReadOnlyList<DealChatNotificationResponse>> GetUnreadNotificationsForCurrentUser(
        string email,
        bool isAdmin,
        bool isClient,
        bool isRealtor,
        int limit);

    Task<int> MarkAllNotificationsAsReadForCurrentUser(
        string email,
        bool isAdmin,
        bool isClient,
        bool isRealtor);

    Task<bool> CanAccessDealChat(
        Guid dealId,
        string email,
        bool isAdmin,
        bool isClient,
        bool isRealtor);
}
