using Microsoft.AspNetCore.Http;
using Web.Models.Api;
using Web.Models.Realtors;

namespace Web.Services;

public class RealtorsApiClient
{
    private readonly HttpClient _httpClient;

    public RealtorsApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<ApiClientResult<IReadOnlyList<RealtorCardViewModel>>> GetAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return GetListAsync($"api/realtors?limit={limit}", cancellationToken);
    }

    public Task<ApiClientResult<IReadOnlyList<RealtorCardViewModel>>> GetTopAsync(
        int count = 6,
        CancellationToken cancellationToken = default)
    {
        return GetListAsync($"api/realtors/top?count={count}", cancellationToken);
    }

    public Task<ApiOperationResult> UpdateLevelModeAsync(
        Guid realtorId,
        bool isLevelManuallyAssigned,
        string? level,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Patch, $"api/realtors/{realtorId}/level-mode", new
        {
            isLevelManuallyAssigned,
            level
        }, cancellationToken);
    }

    private async Task<ApiClientResult<IReadOnlyList<RealtorCardViewModel>>> GetListAsync(
        string relativeUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync(relativeUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<IReadOnlyList<RealtorCardViewModel>>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<List<RealtorDto>>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<IReadOnlyList<RealtorCardViewModel>>.Failure(
                    ApiErrorFactory.Create(
                        StatusCodes.Status500InternalServerError,
                        "API returned an empty realtors payload."));
            }

            var mapped = payload.Select(dto => new RealtorCardViewModel
            {
                Id = dto.Id,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                MiddleName = dto.MiddleName,
                PhoneNumber = dto.PhoneNumber,
                AvatarPath = dto.AvatarPath,
                AverageRating = dto.AverageRating,
                Level = dto.Level,
                IsLevelManuallyAssigned = dto.IsLevelManuallyAssigned,
                DealsThisMonth = dto.DealsThisMonth
            }).ToList();

            return ApiClientResult<IReadOnlyList<RealtorCardViewModel>>.Success(mapped);
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<IReadOnlyList<RealtorCardViewModel>>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<IReadOnlyList<RealtorCardViewModel>>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    private sealed class RealtorDto
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string? AvatarPath { get; set; }
        public double AverageRating { get; set; }
        public int DealsThisMonth { get; set; }
        public string Level { get; set; } = "Undefined";
        public bool IsLevelManuallyAssigned { get; set; }
    }
}
