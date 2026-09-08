using Web.Models.Api;
using Web.Models.Dashboard;

namespace Web.Services;

public class MatchingApiClient
{
    private readonly HttpClient _httpClient;

    public MatchingApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ApiClientResult<DashboardRequirementViewModel>> GetActiveRequirementByClientAsync(
        Guid clientId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/client-requirements/active/by-client/{clientId}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<DashboardRequirementViewModel>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<RequirementDto>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<DashboardRequirementViewModel>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API returned an empty active requirement payload."));
            }

            return ApiClientResult<DashboardRequirementViewModel>.Success(new DashboardRequirementViewModel
            {
                Id = payload.Id,
                ClientId = payload.ClientId,
                DesiredType = payload.DesiredType,
                DesiredTypes = payload.DesiredTypes ?? [],
                Latitude = payload.Latitude,
                Longitude = payload.Longitude,
                SearchRadiusMeters = payload.SearchRadiusMeters,
                IgnoreArea = payload.IgnoreArea,
                MinPrice = payload.MinPrice,
                MaxPrice = payload.MaxPrice,
                MinArea = payload.MinArea,
                MaxArea = payload.MaxArea,
                AddressQuery = payload.AddressQuery,
                MinMatchPercentage = payload.MinMatchPercentage,
                PriceWeight = payload.PriceWeight,
                AreaWeight = payload.AreaWeight,
                IsActive = payload.IsActive
            });
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<DashboardRequirementViewModel>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<DashboardRequirementViewModel>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public async Task<ApiClientResult<IReadOnlyList<DashboardMatchResultViewModel>>> FindMatchesAsync(
        Guid requirementId,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/property-matching/matches")
            {
                Content = JsonContent.Create(new
                {
                    requirementId,
                    limit
                })
            };

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<IReadOnlyList<DashboardMatchResultViewModel>>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<List<MatchDto>>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<IReadOnlyList<DashboardMatchResultViewModel>>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API returned an empty matches payload."));
            }

            var mapped = payload.Select(item => new DashboardMatchResultViewModel
            {
                PropertyId = item.PropertyId,
                Title = item.Title,
                Address = item.Address,
                Price = item.Price,
                OriginalPriceAmount = item.OriginalPriceAmount > 0 ? item.OriginalPriceAmount : item.Price,
                OriginalPriceCurrency = string.IsNullOrWhiteSpace(item.OriginalPriceCurrency) ? "USD" : item.OriginalPriceCurrency,
                Area = item.Area,
                RoomsCount = item.RoomsCount,
                DistanceMeters = item.DistanceMeters,
                MatchScore = item.MatchScore,
                BaseScore = item.Breakdown?.BaseScore ?? 0,
                CriteriaScore = item.Breakdown?.CriteriaScore ?? 0,
                PassedMustHave = item.Breakdown?.PassedMustHave ?? false
            }).ToList();

            return ApiClientResult<IReadOnlyList<DashboardMatchResultViewModel>>.Success(mapped);
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<IReadOnlyList<DashboardMatchResultViewModel>>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<IReadOnlyList<DashboardMatchResultViewModel>>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    private sealed class RequirementDto
    {
        public Guid Id { get; set; }
        public Guid ClientId { get; set; }
        public string DesiredType { get; set; } = "Undefined";
        public List<string>? DesiredTypes { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double SearchRadiusMeters { get; set; }
        public bool IgnoreArea { get; set; }
        public decimal MinPrice { get; set; }
        public decimal MaxPrice { get; set; }
        public double MinArea { get; set; }
        public double? MaxArea { get; set; }
        public string? AddressQuery { get; set; }
        public double MinMatchPercentage { get; set; }
        public double PriceWeight { get; set; }
        public double AreaWeight { get; set; }
        public bool IsActive { get; set; }
    }

    private sealed class MatchDto
    {
        public Guid PropertyId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal OriginalPriceAmount { get; set; }
        public string OriginalPriceCurrency { get; set; } = "USD";
        public double Area { get; set; }
        public int RoomsCount { get; set; }
        public double DistanceMeters { get; set; }
        public double MatchScore { get; set; }
        public MatchBreakdownDto? Breakdown { get; set; }
    }

    private sealed class MatchBreakdownDto
    {
        public double BaseScore { get; set; }
        public double CriteriaScore { get; set; }
        public bool PassedMustHave { get; set; }
    }
}
