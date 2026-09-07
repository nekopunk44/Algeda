using Microsoft.AspNetCore.Http;
using Web.Models.Api;
using Web.Models.Properties;

namespace Web.Services;

public class PropertiesApiClient
{
    private readonly HttpClient _httpClient;

    public PropertiesApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ApiClientResult<IReadOnlyList<PropertySummaryViewModel>>> GetAvailableAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("api/properties/available?limit=500", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<IReadOnlyList<PropertySummaryViewModel>>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<List<PropertyDto>>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<IReadOnlyList<PropertySummaryViewModel>>.Failure(
                    ApiErrorFactory.Create(
                        StatusCodes.Status500InternalServerError,
                        "API returned an empty properties payload."));
            }

            return ApiClientResult<IReadOnlyList<PropertySummaryViewModel>>.Success(payload.Select(MapProperty).ToList());
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<IReadOnlyList<PropertySummaryViewModel>>.Failure(
                ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<IReadOnlyList<PropertySummaryViewModel>>.Failure(
                ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public async Task<ApiClientResult<PropertySummaryViewModel>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/properties/{id}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<PropertySummaryViewModel>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<PropertyDto>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<PropertySummaryViewModel>.Failure(
                    ApiErrorFactory.Create(
                        StatusCodes.Status500InternalServerError,
                        "API returned an empty property payload."));
            }

            return ApiClientResult<PropertySummaryViewModel>.Success(MapProperty(payload));
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<PropertySummaryViewModel>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<PropertySummaryViewModel>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public async Task<ApiClientResult<IReadOnlyList<PropertyCriterionViewModel>>> GetCriteriaAsync(
        Guid propertyId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/properties/{propertyId}/criteria", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<IReadOnlyList<PropertyCriterionViewModel>>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<List<PropertyCriterionDto>>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<IReadOnlyList<PropertyCriterionViewModel>>.Failure(
                    ApiErrorFactory.Create(
                        StatusCodes.Status500InternalServerError,
                        "API returned an empty property criteria payload."));
            }

            var mapped = payload
                .Select(item => new PropertyCriterionViewModel
                {
                    DisplayName = item.CriterionDisplayName,
                    Description = item.CriterionDescription,
                    ValueType = item.CriterionValueType,
                    Category = item.Category,
                    Value = item.Value,
                    DisplayValue = item.DisplayValue
                })
                .ToList();

            return ApiClientResult<IReadOnlyList<PropertyCriterionViewModel>>.Success(mapped);
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<IReadOnlyList<PropertyCriterionViewModel>>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<IReadOnlyList<PropertyCriterionViewModel>>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    private static PropertySummaryViewModel MapProperty(PropertyDto dto)
    {
        return new PropertySummaryViewModel
        {
            Id = dto.Id,
            Title = dto.Title,
            Address = dto.Address,
            Price = dto.Price,
            OriginalPriceAmount = dto.OriginalPriceAmount,
            OriginalPriceCurrency = dto.OriginalPriceCurrency,
            Area = dto.Area,
            RoomsCount = dto.RoomsCount,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            Type = dto.Type,
            Status = dto.Status,
            SoldAtUtc = dto.SoldAtUtc,
            MainPhotoPath = dto.MainPhotoPath,
            PhotoPaths = dto.PhotoPaths ?? [],
            Criteria = (dto.Criteria ?? [])
                .Select(item => new PropertyCriterionViewModel
                {
                    DisplayName = item.CriterionDisplayName,
                    Description = item.CriterionDescription,
                    ValueType = item.CriterionValueType,
                    Category = item.Category,
                    Value = item.Value,
                    DisplayValue = item.DisplayValue
                })
                .ToList()
        };
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
        public string? MainPhotoPath { get; set; }
        public List<string>? PhotoPaths { get; set; }
        public List<PropertyCriterionDto>? Criteria { get; set; }
    }

    private sealed class PropertyCriterionDto
    {
        public string CriterionDisplayName { get; set; } = string.Empty;
        public string? CriterionDescription { get; set; }
        public string CriterionValueType { get; set; } = "Undefined";
        public string? Category { get; set; }
        public string Value { get; set; } = string.Empty;
        public string DisplayValue { get; set; } = string.Empty;
    }
}
