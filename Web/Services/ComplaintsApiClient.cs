using Web.Models.Api;
using Web.Models.Profile;

namespace Web.Services;

public class ComplaintsApiClient
{
    private readonly HttpClient _httpClient;

    public ComplaintsApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<ApiOperationResult> CreateComplaintAsync(
        string category,
        string subject,
        string description,
        Guid? dealId = null,
        Guid? propertyId = null,
        Guid? targetRealtorId = null,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Post, "api/complaints", new
        {
            category,
            subject,
            description,
            dealId,
            propertyId,
            targetRealtorId
        }, cancellationToken);
    }

    public async Task<ApiClientResult<IReadOnlyList<ProfileComplaintViewModel>>> GetMyRealtorComplaintsAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var safeLimit = Math.Clamp(limit, 1, 500);
            var response = await _httpClient.GetAsync($"api/complaints/my-realtor?limit={safeLimit}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<IReadOnlyList<ProfileComplaintViewModel>>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<List<ComplaintDto>>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<IReadOnlyList<ProfileComplaintViewModel>>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API returned an empty realtor complaints payload."));
            }

            var mapped = payload
                .OrderByDescending(x => x.CreatedDate)
                .Select(x => new ProfileComplaintViewModel
                {
                    Id = x.Id,
                    Category = x.Category,
                    Subject = x.Subject,
                    Description = x.Description,
                    Status = x.Status,
                    ModerationVerdict = x.ModerationVerdict,
                    DealId = x.DealId,
                    PropertyId = x.PropertyId,
                    AdminResolution = x.AdminResolution,
                    CreatedDate = x.CreatedDate,
                    ResolvedAt = x.ResolvedAt
                })
                .ToList();

            return ApiClientResult<IReadOnlyList<ProfileComplaintViewModel>>.Success(mapped);
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<IReadOnlyList<ProfileComplaintViewModel>>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<IReadOnlyList<ProfileComplaintViewModel>>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    private sealed class ComplaintDto
    {
        public Guid Id { get; set; }
        public Guid? DealId { get; set; }
        public Guid? PropertyId { get; set; }
        public string Category { get; set; } = "Undefined";
        public string Subject { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = "Undefined";
        public string ModerationVerdict { get; set; } = "Undefined";
        public string? AdminResolution { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
