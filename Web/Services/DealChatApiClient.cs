using Web.Models.Api;
using Web.Models.DealChat;

namespace Web.Services;

public sealed class DealChatApiClient
{
    private readonly HttpClient _httpClient;

    public DealChatApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ApiClientResult<DealChatDialogViewModel>> GetDealDialogAsync(
        Guid dealId,
        int limit = 200,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var safeLimit = Math.Clamp(limit, 1, 1000);
            var response = await _httpClient.GetAsync($"api/deal-chats/{dealId}/messages?limit={safeLimit}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<DealChatDialogViewModel>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<DealChatDialogDto>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<DealChatDialogViewModel>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API returned an empty deal chat payload."));
            }

            return ApiClientResult<DealChatDialogViewModel>.Success(MapDialog(payload));
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<DealChatDialogViewModel>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<DealChatDialogViewModel>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public Task<ApiOperationResult> SendMessageAsync(
        Guid dealId,
        string content,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Post, $"api/deal-chats/{dealId}/messages", new
        {
            content
        }, cancellationToken);
    }

    public async Task<ApiClientResult<IReadOnlyList<DealChatNotificationViewModel>>> GetUnreadNotificationsAsync(
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var safeLimit = Math.Clamp(limit, 1, 200);
            var response = await _httpClient.GetAsync(
                $"api/deal-chats/notifications/unread?limit={safeLimit}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<IReadOnlyList<DealChatNotificationViewModel>>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<List<DealChatNotificationDto>>(
                ApiClientSupport.JsonOptions,
                cancellationToken);

            if (payload is null)
            {
                return ApiClientResult<IReadOnlyList<DealChatNotificationViewModel>>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API returned an empty chat notification payload."));
            }

            var mapped = payload
                .OrderByDescending(x => x.LastMessageAtUtc)
                .Select(x => new DealChatNotificationViewModel
                {
                    DealId = x.DealId,
                    CounterpartyName = x.CounterpartyName,
                    Preview = x.Preview,
                    UnreadCount = x.UnreadCount,
                    LastMessageAtUtc = x.LastMessageAtUtc
                })
                .ToList();

            return ApiClientResult<IReadOnlyList<DealChatNotificationViewModel>>.Success(mapped);
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<IReadOnlyList<DealChatNotificationViewModel>>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<IReadOnlyList<DealChatNotificationViewModel>>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public Task<ApiOperationResult> MarkNotificationsReadAsync(CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Post, "api/deal-chats/notifications/mark-read", null, cancellationToken);
    }

    public async Task<ApiClientResult<IReadOnlyList<DealChatSummaryViewModel>>> GetAdminChatsAsync(
        Guid? dealId,
        Guid? clientId,
        Guid? realtorId,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new List<string> { $"limit={Math.Clamp(limit, 1, 200)}" };
            if (dealId.HasValue)
                query.Add($"dealId={dealId.Value}");
            if (clientId.HasValue)
                query.Add($"clientId={clientId.Value}");
            if (realtorId.HasValue)
                query.Add($"realtorId={realtorId.Value}");

            var response = await _httpClient.GetAsync($"api/deal-chats/admin?{string.Join("&", query)}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<IReadOnlyList<DealChatSummaryViewModel>>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<List<DealChatSummaryDto>>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<IReadOnlyList<DealChatSummaryViewModel>>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API returned an empty admin chat list payload."));
            }

            var mapped = payload
                .Select(MapSummary)
                .OrderByDescending(x => x.LastMessageAtUtc)
                .ToList();

            return ApiClientResult<IReadOnlyList<DealChatSummaryViewModel>>.Success(mapped);
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<IReadOnlyList<DealChatSummaryViewModel>>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<IReadOnlyList<DealChatSummaryViewModel>>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    private static DealChatDialogViewModel MapDialog(DealChatDialogDto payload)
    {
        return new DealChatDialogViewModel
        {
            DealId = payload.DealId,
            ClientId = payload.ClientId,
            RealtorId = payload.RealtorId,
            CurrentActorId = payload.CurrentActorId,
            ClientName = payload.ClientName,
            RealtorName = payload.RealtorName,
            CanWrite = payload.CanWrite,
            BlockReason = payload.BlockReason,
            Messages = (payload.Messages ?? [])
                .OrderBy(x => x.CreatedDate)
                .Select(x => new DealChatMessageViewModel
                {
                    Id = x.Id,
                    DealId = x.DealId,
                    SenderId = x.SenderId,
                    ReceiverId = x.ReceiverId,
                    Content = x.Content,
                    IsRead = x.IsRead,
                    CreatedDate = x.CreatedDate,
                    IsOutgoing = x.IsOutgoing
                })
                .ToList()
        };
    }

    private static DealChatSummaryViewModel MapSummary(DealChatSummaryDto payload)
    {
        return new DealChatSummaryViewModel
        {
            DealId = payload.DealId,
            ClientId = payload.ClientId,
            ClientName = payload.ClientName,
            RealtorId = payload.RealtorId,
            RealtorName = payload.RealtorName,
            DealStatus = payload.DealStatus,
            LastMessageAtUtc = payload.LastMessageAtUtc,
            LastMessagePreview = payload.LastMessagePreview,
            MessageCount = payload.MessageCount
        };
    }

    private sealed class DealChatDialogDto
    {
        public Guid DealId { get; set; }
        public Guid ClientId { get; set; }
        public Guid RealtorId { get; set; }
        public Guid CurrentActorId { get; set; }
        public string? ClientName { get; set; }
        public string? RealtorName { get; set; }
        public bool CanWrite { get; set; }
        public string? BlockReason { get; set; }
        public List<DealChatMessageDto>? Messages { get; set; }
    }

    private sealed class DealChatMessageDto
    {
        public Guid Id { get; set; }
        public Guid DealId { get; set; }
        public Guid SenderId { get; set; }
        public Guid ReceiverId { get; set; }
        public string Content { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsOutgoing { get; set; }
    }

    private sealed class DealChatSummaryDto
    {
        public Guid DealId { get; set; }
        public Guid ClientId { get; set; }
        public string? ClientName { get; set; }
        public Guid RealtorId { get; set; }
        public string? RealtorName { get; set; }
        public string DealStatus { get; set; } = "Undefined";
        public DateTime? LastMessageAtUtc { get; set; }
        public string? LastMessagePreview { get; set; }
        public int MessageCount { get; set; }
    }

    private sealed class DealChatNotificationDto
    {
        public Guid DealId { get; set; }
        public string? CounterpartyName { get; set; }
        public string Preview { get; set; } = string.Empty;
        public int UnreadCount { get; set; }
        public DateTime LastMessageAtUtc { get; set; }
    }
}
