using Application.DTOs.DealChat;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Entities;

namespace Application.Services;

public sealed class DealChatService : IDealChatService
{
    private readonly IChatRepository _chatRepository;
    private readonly IDealRepository _dealRepository;
    private readonly IClientRepository _clientRepository;
    private readonly IRealtorRepository _realtorRepository;
    private readonly DealService _dealService;

    public DealChatService(
        IChatRepository chatRepository,
        IDealRepository dealRepository,
        IClientRepository clientRepository,
        IRealtorRepository realtorRepository,
        DealService dealService)
    {
        _chatRepository = chatRepository;
        _dealRepository = dealRepository;
        _clientRepository = clientRepository;
        _realtorRepository = realtorRepository;
        _dealService = dealService;
    }

    public async Task<DealChatDialogResponse> GetDialogForCurrentUser(
        Guid dealId,
        string email,
        bool isAdmin,
        bool isClient,
        bool isRealtor,
        int limit)
    {
        var deal = await GetDealOrThrow(dealId);
        var access = await ResolveAccessOrThrow(deal, email, isAdmin, isClient, isRealtor, forWrite: false);

        if (access.ActorId != Guid.Empty)
        {
            await _chatRepository.MarkDealMessagesAsRead(dealId, access.ActorId);
        }

        var safeLimit = Math.Clamp(limit, 1, 1000);
        var messages = await _chatRepository.GetDealDialog(dealId, safeLimit);

        var client = await _clientRepository.GetById(deal.ClientId);
        var realtor = await _realtorRepository.GetById(deal.RealtorId);

        var mapped = messages
            .Select(x => new DealChatMessageResponse(
                x.Id,
                x.DealId ?? dealId,
                x.SenderId,
                x.ReceiverId,
                x.Content,
                x.IsRead,
                x.CreatedDate,
                x.SenderId == access.ActorId))
            .ToList();

        return new DealChatDialogResponse(
            dealId,
            deal.ClientId,
            deal.RealtorId,
            access.ActorId,
            client?.FullName.ToString(),
            realtor?.FullName.ToString(),
            access.CanWrite,
            access.BlockReason,
            mapped);
    }

    public async Task<DealChatMessageResponse> SendMessageForCurrentUser(
        Guid dealId,
        string email,
        bool isAdmin,
        bool isClient,
        bool isRealtor,
        string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ValidationException("Текст сообщения обязателен.");

        var deal = await GetDealOrThrow(dealId);
        var access = await ResolveAccessOrThrow(deal, email, isAdmin, isClient, isRealtor, forWrite: true);

        if (!access.CanWrite)
            throw new ValidationException(access.BlockReason ?? "Нельзя отправлять сообщения в этом чате.");

        var receiverId = access.ActorId == deal.ClientId
            ? deal.RealtorId
            : deal.ClientId;

        var message = new ChatMessage(
            access.ActorId,
            receiverId,
            content.Trim(),
            dealId);

        await _chatRepository.Add(message);

        return new DealChatMessageResponse(
            message.Id,
            dealId,
            message.SenderId,
            message.ReceiverId,
            message.Content,
            message.IsRead,
            message.CreatedDate,
            true);
    }

    public async Task<IReadOnlyList<DealChatSummaryResponse>> GetAdminChats(
        Guid? dealId,
        Guid? clientId,
        Guid? realtorId,
        int limit)
    {
        var safeLimit = Math.Clamp(limit, 1, 200);
        var scanLimit = Math.Max(safeLimit * 4, 200);

        var deals = await _dealRepository.Get(scanLimit);

        if (dealId.HasValue)
            deals = deals.Where(x => x.Id == dealId.Value).ToList();

        if (clientId.HasValue)
            deals = deals.Where(x => x.ClientId == clientId.Value).ToList();

        if (realtorId.HasValue)
            deals = deals.Where(x => x.RealtorId == realtorId.Value).ToList();

        deals = deals
            .Where(x => x.RealtorId != Guid.Empty)
            .ToList();

        var clientIds = deals.Select(x => x.ClientId).Distinct().ToList();
        var realtorIds = deals.Select(x => x.RealtorId).Distinct().ToList();

        var clientsById = new Dictionary<Guid, string?>();
        foreach (var id in clientIds)
        {
            var client = await _clientRepository.GetById(id);
            clientsById[id] = client?.FullName.ToString();
        }

        var realtorsById = new Dictionary<Guid, string?>();
        foreach (var id in realtorIds)
        {
            var realtor = await _realtorRepository.GetById(id);
            realtorsById[id] = realtor?.FullName.ToString();
        }

        var items = new List<DealChatSummaryResponse>();

        foreach (var deal in deals)
        {
            var latest = await _chatRepository.GetDealDialog(deal.Id, 1);
            if (latest.Count == 0)
                continue;

            var last = latest[0];
            var count = await _chatRepository.CountDealMessages(deal.Id);

            items.Add(new DealChatSummaryResponse(
                deal.Id,
                deal.ClientId,
                clientsById.GetValueOrDefault(deal.ClientId),
                deal.RealtorId,
                realtorsById.GetValueOrDefault(deal.RealtorId),
                deal.Status.ToString(),
                last.CreatedDate,
                BuildPreview(last.Content),
                count));
        }

        return items
            .OrderByDescending(x => x.LastMessageAtUtc)
            .Take(safeLimit)
            .ToList();
    }

    public async Task<IReadOnlyList<DealChatNotificationResponse>> GetUnreadNotificationsForCurrentUser(
        string email,
        bool isAdmin,
        bool isClient,
        bool isRealtor,
        int limit)
    {
        var actorId = await ResolveCurrentActorId(email, isAdmin, isClient, isRealtor);
        if (actorId == Guid.Empty)
            return [];

        var unread = await _chatRepository.GetUnreadByReceiver(actorId, Math.Clamp(limit, 1, 200));
        if (unread.Count == 0)
            return [];

        var dealIds = unread
            .Where(x => x.DealId.HasValue)
            .Select(x => x.DealId!.Value)
            .Distinct()
            .ToList();

        var notifications = new List<DealChatNotificationResponse>(dealIds.Count);
        foreach (var dealId in dealIds)
        {
            var deal = await _dealRepository.GetById(dealId);
            if (deal is null || deal.RealtorId == Guid.Empty)
                continue;

            var items = unread
                .Where(x => x.DealId == dealId)
                .OrderByDescending(x => x.CreatedDate)
                .ToList();

            var last = items.First();
            var counterpartyName = await ResolveCounterpartyName(actorId, deal);

            notifications.Add(new DealChatNotificationResponse(
                dealId,
                counterpartyName,
                BuildPreview(last.Content) ?? "Новое сообщение",
                items.Count,
                last.CreatedDate));
        }

        return notifications
            .OrderByDescending(x => x.LastMessageAtUtc)
            .ToList();
    }

    public async Task<int> MarkAllNotificationsAsReadForCurrentUser(
        string email,
        bool isAdmin,
        bool isClient,
        bool isRealtor)
    {
        var actorId = await ResolveCurrentActorId(email, isAdmin, isClient, isRealtor);
        if (actorId == Guid.Empty)
            return 0;

        return await _chatRepository.MarkAllMessagesAsRead(actorId);
    }

    public async Task<bool> CanAccessDealChat(
        Guid dealId,
        string email,
        bool isAdmin,
        bool isClient,
        bool isRealtor)
    {
        var deal = await _dealRepository.GetById(dealId);
        if (deal is null || deal.RealtorId == Guid.Empty)
            return false;

        try
        {
            _ = await ResolveAccessOrThrow(deal, email, isAdmin, isClient, isRealtor, forWrite: false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<Deal> GetDealOrThrow(Guid dealId)
    {
        if (dealId == Guid.Empty)
            throw new ValidationException("Не указан идентификатор сделки.");

        var deal = await _dealRepository.GetById(dealId);
        if (deal is null)
            throw new NotFoundException("Чат по сделке не найден.");

        if (deal.RealtorId == Guid.Empty)
            throw new ValidationException("Чат станет доступен после назначения риелтора.");

        return deal;
    }

    private async Task<ChatAccessResult> ResolveAccessOrThrow(
        Deal deal,
        string email,
        bool isAdmin,
        bool isClient,
        bool isRealtor,
        bool forWrite)
    {
        if (isAdmin)
        {
            return forWrite
                ? new ChatAccessResult(Guid.Empty, false, "Администратор может только просматривать чат.")
                : new ChatAccessResult(Guid.Empty, false, null);
        }

        if (isClient)
        {
            var clientId = await _dealService.ResolveClientIdByEmail(email);
            if (clientId == deal.ClientId)
            {
                return new ChatAccessResult(clientId, true, null);
            }
        }

        if (isRealtor)
        {
            var realtorId = await _dealService.ResolveRealtorIdByEmail(email);
            if (realtorId == deal.RealtorId)
            {
                return new ChatAccessResult(realtorId, true, null);
            }
        }

        throw new NotFoundException("Чат по сделке не найден.");
    }

    private static string? BuildPreview(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        var normalized = content.Trim();
        return normalized.Length <= 140
            ? normalized
            : normalized[..140] + "...";
    }

    private async Task<Guid> ResolveCurrentActorId(string email, bool isAdmin, bool isClient, bool isRealtor)
    {
        if (isAdmin)
            return Guid.Empty;

        if (isClient)
            return await _dealService.ResolveClientIdByEmail(email);

        if (isRealtor)
            return await _dealService.ResolveRealtorIdByEmail(email);

        throw new ValidationException("Пользователь не может работать с уведомлениями чата.");
    }

    private async Task<string?> ResolveCounterpartyName(Guid actorId, Deal deal)
    {
        if (actorId == deal.ClientId)
        {
            var realtor = await _realtorRepository.GetById(deal.RealtorId);
            return realtor?.FullName.ToString();
        }

        if (actorId == deal.RealtorId)
        {
            var client = await _clientRepository.GetById(deal.ClientId);
            return client?.FullName.ToString();
        }

        return null;
    }

    private sealed record ChatAccessResult(Guid ActorId, bool CanWrite, string? BlockReason);
}
