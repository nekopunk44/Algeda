using Web.Models.Api;
using Web.Models.Dashboard;
using Web.Models.Properties;

namespace Web.Services;

public class RealtorDashboardApiClient
{
    private readonly HttpClient _httpClient;

    public RealtorDashboardApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ApiClientResult<IReadOnlyList<PropertySummaryViewModel>>> GetPropertiesAsync(
        int limit = 200,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/properties?limit={limit}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<IReadOnlyList<PropertySummaryViewModel>>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<List<PropertyDto>>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<IReadOnlyList<PropertySummaryViewModel>>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API returned an empty properties payload."));
            }

            var mapped = payload.Select(item => new PropertySummaryViewModel
            {
                Id = item.Id,
                Title = item.Title,
                Address = item.Address,
                Price = item.Price,
                OriginalPriceAmount = item.OriginalPriceAmount,
                OriginalPriceCurrency = item.OriginalPriceCurrency,
                Area = item.Area,
                RoomsCount = item.RoomsCount,
                Latitude = item.Latitude,
                Longitude = item.Longitude,
                Type = item.Type,
                Status = item.Status,
                SoldAtUtc = item.SoldAtUtc,
                OwnerFullName = item.OwnerFullName,
                OwnerEmail = item.OwnerEmail,
                OwnerPhoneNumber = item.OwnerPhoneNumber,
                ResponsibleRealtorId = item.ResponsibleRealtorId,
                MainPhotoPath = item.MainPhotoPath,
                PhotoPaths = item.PhotoPaths ?? [],
                Criteria = (item.Criteria ?? [])
                    .Select(x => new PropertyCriterionViewModel
                    {
                        DisplayName = x.CriterionDisplayName,
                        ValueType = "Undefined",
                        Value = x.Value,
                        DisplayValue = x.DisplayValue
                    })
                    .ToList(),
                CreatedDate = item.CreatedDate
            }).ToList();

            return ApiClientResult<IReadOnlyList<PropertySummaryViewModel>>.Success(mapped);
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<IReadOnlyList<PropertySummaryViewModel>>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<IReadOnlyList<PropertySummaryViewModel>>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public Task<ApiOperationResult> CreatePropertyAsync(
        string title,
        string address,
        decimal price,
        double area,
        int roomsCount,
        double latitude,
        double longitude,
        string type,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Post, "api/properties", new
        {
            title,
            address,
            price,
            priceCurrency = "USD",
            area,
            roomsCount,
            latitude,
            longitude,
            type
        }, cancellationToken);
    }

    public Task<ApiOperationResult> UpdatePropertyPriceAsync(
        Guid propertyId,
        decimal newPrice,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Put, $"api/properties/{propertyId}/price", new
        {
            propertyId,
            newPrice
        }, cancellationToken);
    }

    public Task<ApiOperationResult> MarkPropertyAsSoldAsync(
        Guid propertyId,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Patch, $"api/properties/{propertyId}/mark-as-sold", null, cancellationToken);
    }

    public Task<ApiClientResult<IReadOnlyList<DashboardDealViewModel>>> GetDealsAsync(
        int limit = 200,
        CancellationToken cancellationToken = default)
    {
        return GetDealsCoreAsync($"api/deals?limit={limit}", cancellationToken);
    }

    public Task<ApiClientResult<IReadOnlyList<DashboardDealViewModel>>> GetDealsByRealtorAsync(
        Guid realtorId,
        CancellationToken cancellationToken = default)
    {
        return GetDealsCoreAsync($"api/deals/by-realtor/{realtorId}", cancellationToken);
    }

    public Task<ApiClientResult<IReadOnlyList<DashboardDealViewModel>>> GetDealsByClientAsync(
        Guid clientId,
        CancellationToken cancellationToken = default)
    {
        return GetDealsCoreAsync($"api/deals/by-client/{clientId}", cancellationToken);
    }

    public Task<ApiOperationResult> CompleteDealAsync(
        Guid dealId,
        decimal commissionAmount,
        string commissionCurrency,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Patch, $"api/deals/{dealId}/complete", new
        {
            dealId,
            commissionAmount,
            commissionCurrency
        }, cancellationToken);
    }

    public Task<ApiOperationResult> CancelDealAsync(
        Guid dealId,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Patch, $"api/deals/{dealId}/cancel", null, cancellationToken);
    }

    public Task<ApiClientResult<IReadOnlyList<DashboardActivityLogViewModel>>> GetActivityLogsAsync(
        int limit = 200,
        CancellationToken cancellationToken = default)
    {
        return GetActivityLogsCoreAsync($"api/realtor-activity-logs?limit={limit}", cancellationToken);
    }

    public Task<ApiClientResult<IReadOnlyList<DashboardActivityLogViewModel>>> GetActivityLogsByRealtorAsync(
        Guid realtorId,
        CancellationToken cancellationToken = default)
    {
        return GetActivityLogsCoreAsync($"api/realtor-activity-logs/by-realtor/{realtorId}", cancellationToken);
    }

    public Task<ApiOperationResult> CreateActivityLogAsync(
        Guid realtorId,
        string type,
        int points,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Post, "api/realtor-activity-logs", new
        {
            realtorId,
            type,
            points
        }, cancellationToken);
    }

    public Task<ApiOperationResult> UpdateActivityLogAsync(
        Guid activityLogId,
        string type,
        int points,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Put, $"api/realtor-activity-logs/{activityLogId}", new
        {
            type,
            points
        }, cancellationToken);
    }

    public Task<ApiOperationResult> DeleteActivityLogAsync(
        Guid activityLogId,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Delete, $"api/realtor-activity-logs/{activityLogId}", null, cancellationToken);
    }

    public async Task<ApiClientResult<int>> GetSubscribedClientsCountAsync(
        Guid propertyId,
        int requirementsLimit = 500,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"api/property-matching/properties/{propertyId}/subscribed-clients?requirementsLimit={requirementsLimit}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<int>.Failure(await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<List<Guid>>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<int>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API returned an empty subscribers payload."));
            }

            return ApiClientResult<int>.Success(payload.Count);
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<int>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<int>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    private async Task<ApiClientResult<IReadOnlyList<DashboardDealViewModel>>> GetDealsCoreAsync(
        string relativeUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync(relativeUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<IReadOnlyList<DashboardDealViewModel>>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<List<DealDto>>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<IReadOnlyList<DashboardDealViewModel>>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API returned an empty deals payload."));
            }

            var mapped = payload.Select(item => new DashboardDealViewModel
            {
                Id = item.Id,
                PropertyId = item.PropertyId,
                ClientId = item.ClientId,
                RealtorId = item.RealtorId,
                Status = item.Status,
                CommissionAmount = item.CommissionAmount,
                CommissionCurrency = item.CommissionCurrency,
                RealtorCommissionPercent = item.RealtorCommissionPercent,
                RealtorPayoutAmount = item.RealtorPayoutAmount,
                RealtorPayoutCurrency = item.RealtorPayoutCurrency,
                AgencyNetCommissionAmount = item.AgencyNetCommissionAmount,
                AgencyNetCommissionCurrency = item.AgencyNetCommissionCurrency,
                CompletedAt = item.CompletedAt,
                CreatedDate = item.CreatedDate
            }).ToList();

            return ApiClientResult<IReadOnlyList<DashboardDealViewModel>>.Success(mapped);
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<IReadOnlyList<DashboardDealViewModel>>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<IReadOnlyList<DashboardDealViewModel>>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    private async Task<ApiClientResult<IReadOnlyList<DashboardActivityLogViewModel>>> GetActivityLogsCoreAsync(
        string relativeUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync(relativeUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<IReadOnlyList<DashboardActivityLogViewModel>>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<List<ActivityLogDto>>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<IReadOnlyList<DashboardActivityLogViewModel>>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API returned an empty activity logs payload."));
            }

            var mapped = payload.Select(item => new DashboardActivityLogViewModel
            {
                Id = item.Id,
                RealtorId = item.RealtorId,
                Type = item.Type,
                Points = item.Points,
                CreatedDate = item.CreatedDate
            }).ToList();

            return ApiClientResult<IReadOnlyList<DashboardActivityLogViewModel>>.Success(mapped);
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<IReadOnlyList<DashboardActivityLogViewModel>>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<IReadOnlyList<DashboardActivityLogViewModel>>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    private sealed class PropertyDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal OriginalPriceAmount { get; set; }
        public string OriginalPriceCurrency { get; set; } = "USD";
        public double Area { get; set; }
        public int RoomsCount { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Type { get; set; } = "Undefined";
        public string Status { get; set; } = "Undefined";
        public DateTime? SoldAtUtc { get; set; }
        public string? OwnerFullName { get; set; }
        public string? OwnerEmail { get; set; }
        public string? OwnerPhoneNumber { get; set; }
        public Guid? ResponsibleRealtorId { get; set; }
        public string? MainPhotoPath { get; set; }
        public List<string>? PhotoPaths { get; set; }
        public List<PropertyCriterionDto>? Criteria { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    private sealed class PropertyCriterionDto
    {
        public Guid CriterionDefinitionId { get; set; }
        public string CriterionDisplayName { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string DisplayValue { get; set; } = string.Empty;
    }

    private sealed class DealDto
    {
        public Guid Id { get; set; }
        public Guid PropertyId { get; set; }
        public Guid ClientId { get; set; }
        public Guid RealtorId { get; set; }
        public string Status { get; set; } = "Undefined";
        public decimal CommissionAmount { get; set; }
        public string CommissionCurrency { get; set; } = "USD";
        public decimal RealtorCommissionPercent { get; set; }
        public decimal RealtorPayoutAmount { get; set; }
        public string RealtorPayoutCurrency { get; set; } = "USD";
        public decimal AgencyNetCommissionAmount { get; set; }
        public string AgencyNetCommissionCurrency { get; set; } = "USD";
        public DateTime? CompletedAt { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    private sealed class ActivityLogDto
    {
        public Guid Id { get; set; }
        public Guid RealtorId { get; set; }
        public string Type { get; set; } = "Undefined";
        public int Points { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
