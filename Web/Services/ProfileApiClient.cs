using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Web.Models.Api;
using Web.Models.Profile;

namespace Web.Services;

public sealed class ProfileApiClient
{
    private readonly HttpClient _httpClient;

    public ProfileApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ApiClientResult<ProfileDetailsViewModel>> GetMyProfileAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("api/account/me", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<ProfileDetailsViewModel>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<ProfileDetailsDto>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<ProfileDetailsViewModel>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API returned an empty account payload."));
            }

            return ApiClientResult<ProfileDetailsViewModel>.Success(Map(payload));
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<ProfileDetailsViewModel>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<ProfileDetailsViewModel>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public async Task<ApiClientResult<ProfileDetailsViewModel>> UpdateMyProfileAsync(
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync("api/account/me", request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<ProfileDetailsViewModel>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<ProfileDetailsDto>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<ProfileDetailsViewModel>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API returned an empty account payload."));
            }

            return ApiClientResult<ProfileDetailsViewModel>.Success(Map(payload));
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<ProfileDetailsViewModel>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<ProfileDetailsViewModel>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public Task<ApiOperationResult> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(
            _httpClient,
            HttpMethod.Post,
            "api/account/change-password",
            request,
            cancellationToken);
    }

    public Task<ApiOperationResult> RequestEmailChangeAsync(
        RequestEmailChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(
            _httpClient,
            HttpMethod.Post,
            "api/account/request-email-change",
            request,
            cancellationToken);
    }

    public async Task<ApiClientResult<ProfileDetailsViewModel>> ConfirmEmailChangeAsync(
        ConfirmEmailChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/account/confirm-email-change", request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<ProfileDetailsViewModel>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<ProfileDetailsDto>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<ProfileDetailsViewModel>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API returned an empty account payload."));
            }

            return ApiClientResult<ProfileDetailsViewModel>.Success(Map(payload));
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<ProfileDetailsViewModel>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<ProfileDetailsViewModel>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public async Task<ApiClientResult<string>> UploadAvatarAsync(IFormFile avatar, CancellationToken cancellationToken = default)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            await using var stream = avatar.OpenReadStream();
            var streamContent = new StreamContent(stream);
            if (!string.IsNullOrWhiteSpace(avatar.ContentType))
            {
                streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(avatar.ContentType);
            }

            form.Add(streamContent, "avatar", avatar.FileName);

            var response = await _httpClient.PostAsync("api/account/avatar", form, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<string>.Failure(await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<AvatarUploadDto>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null || string.IsNullOrWhiteSpace(payload.Path))
            {
                return ApiClientResult<string>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API returned an empty avatar upload payload."));
            }

            return ApiClientResult<string>.Success(payload.Path);
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<string>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<string>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public Task<ApiOperationResult> RemoveAvatarAsync(CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Delete, "api/account/avatar", null, cancellationToken);
    }

    public async Task<ApiClientResult<IReadOnlyList<AccountSessionViewModel>>> GetMySessionsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("api/account/sessions", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<IReadOnlyList<AccountSessionViewModel>>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<List<AccountSessionDto>>(
                ApiClientSupport.JsonOptions,
                cancellationToken);

            var sessions = (payload ?? [])
                .Select(x => new AccountSessionViewModel
                {
                    Id = x.Id,
                    DeviceName = x.DeviceName,
                    IpAddress = x.IpAddress,
                    CreatedAtUtc = x.CreatedAtUtc,
                    LastSeenAtUtc = x.LastSeenAtUtc,
                    ExpiresAtUtc = x.ExpiresAtUtc,
                    IsCurrent = x.IsCurrent
                })
                .ToArray();

            return ApiClientResult<IReadOnlyList<AccountSessionViewModel>>.Success(sessions);
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<IReadOnlyList<AccountSessionViewModel>>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<IReadOnlyList<AccountSessionViewModel>>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public Task<ApiOperationResult> RevokeSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(
            _httpClient,
            HttpMethod.Delete,
            $"api/account/sessions/{sessionId}",
            null,
            cancellationToken);
    }

    private static ProfileDetailsViewModel Map(ProfileDetailsDto dto)
    {
        return new ProfileDetailsViewModel
        {
            Email = dto.Email,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            MiddleName = dto.MiddleName,
            PhoneNumber = dto.PhoneNumber,
            IsRealtor = dto.IsRealtor,
            IsClient = dto.IsClient,
            IsAdmin = dto.IsAdmin,
            AvatarPath = dto.AvatarPath,
            RealtorLevel = dto.RealtorLevel,
            IsLevelManuallyAssigned = dto.IsLevelManuallyAssigned,
            RealtorCommissionPercent = dto.RealtorCommissionPercent,
            RealtorPayoutThisMonth = dto.RealtorPayoutThisMonth,
            RealtorPayoutTotal = dto.RealtorPayoutTotal,
            RealtorPayoutCurrency = dto.RealtorPayoutCurrency
        };
    }

    private sealed class ProfileDetailsDto
    {
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public bool IsRealtor { get; set; }
        public bool IsClient { get; set; }
        public bool IsAdmin { get; set; }
        public string? AvatarPath { get; set; }
        public string? RealtorLevel { get; set; }
        public bool? IsLevelManuallyAssigned { get; set; }
        public decimal? RealtorCommissionPercent { get; set; }
        public decimal? RealtorPayoutThisMonth { get; set; }
        public decimal? RealtorPayoutTotal { get; set; }
        public string RealtorPayoutCurrency { get; set; } = "USD";
    }

    private sealed class AvatarUploadDto
    {
        public string Path { get; set; } = string.Empty;
    }

    private sealed class AccountSessionDto
    {
        public Guid Id { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public string? IpAddress { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime LastSeenAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public bool IsCurrent { get; set; }
    }
}
