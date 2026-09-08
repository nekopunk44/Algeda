using Microsoft.AspNetCore.Http;
using Web.Models.Api;
using Web.Models.RealEstate;

namespace Web.Services;

public class RealEstateApiClient
{
    private readonly HttpClient _httpClient;

    public RealEstateApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ApiClientResult<IReadOnlyList<RealEstatePropertyCardViewModel>>> GetPropertiesAsync(
        int limit = 500,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/properties?limit={limit}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<IReadOnlyList<RealEstatePropertyCardViewModel>>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<List<PropertyDto>>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<IReadOnlyList<RealEstatePropertyCardViewModel>>.Failure(
                    ApiErrorFactory.Create(
                        StatusCodes.Status500InternalServerError,
                        "API вернул пустой список объектов."));
            }

            return ApiClientResult<IReadOnlyList<RealEstatePropertyCardViewModel>>.Success(
                payload.Select(MapPropertyCard).ToList());
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<IReadOnlyList<RealEstatePropertyCardViewModel>>.Failure(
                ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<IReadOnlyList<RealEstatePropertyCardViewModel>>.Failure(
                ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public async Task<ApiClientResult<IReadOnlyList<CurrencyRateViewModel>>> GetCurrenciesAsync(
        int limit = 200,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"api/currencies?limit={Math.Clamp(limit, 1, 500)}&includeInactive={(includeInactive ? "true" : "false")}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<IReadOnlyList<CurrencyRateViewModel>>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<List<CurrencyRateDto>>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<IReadOnlyList<CurrencyRateViewModel>>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API ������ ������ ������ �����."));
            }

            var mapped = payload
                .OrderBy(x => x.Code)
                .Select(x => new CurrencyRateViewModel
                {
                    Id = x.Id,
                    Code = x.Code,
                    Name = x.Name,
                    Symbol = x.Symbol,
                    RateToBase = x.RateToBase,
                    IsActive = x.IsActive,
                    UpdatedAtUtc = x.UpdatedAtUtc
                })
                .ToList();

            return ApiClientResult<IReadOnlyList<CurrencyRateViewModel>>.Success(mapped);
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<IReadOnlyList<CurrencyRateViewModel>>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<IReadOnlyList<CurrencyRateViewModel>>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public async Task<ApiClientResult<RealEstatePropertyManagementViewModel>> GetPropertyForManagementAsync(
        Guid propertyId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/properties/{propertyId}/management", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<RealEstatePropertyManagementViewModel>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<PropertyManagementDto>(
                ApiClientSupport.JsonOptions,
                cancellationToken);

            if (payload is null)
            {
                return ApiClientResult<RealEstatePropertyManagementViewModel>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API вернул пустой объект недвижимости."));
            }

            return ApiClientResult<RealEstatePropertyManagementViewModel>.Success(MapPropertyManagement(payload));
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<RealEstatePropertyManagementViewModel>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<RealEstatePropertyManagementViewModel>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public async Task<ApiClientResult<Guid>> CreatePropertyAsync(
        string title,
        string address,
        decimal price,
        string priceCurrency,
        double area,
        int roomsCount,
        double latitude,
        double longitude,
        string type,
        string? ownerFullName,
        string? ownerEmail,
        string? ownerPhoneNumber,
        Guid? ownerClientId,
        IReadOnlyList<string> photoPaths,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/properties", new
            {
                title,
                address,
                price,
                priceCurrency,
                area,
                roomsCount,
                latitude,
                longitude,
                type,
                photoPaths,
                ownerFullName,
                ownerEmail,
                ownerPhoneNumber,
                ownerClientId
            }, ApiClientSupport.JsonOptions, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<Guid>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<PropertyDto>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null || payload.Id == Guid.Empty)
            {
                return ApiClientResult<Guid>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API не вернул идентификатор созданного объекта."));
            }

            return ApiClientResult<Guid>.Success(payload.Id);
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<Guid>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<Guid>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public Task<ApiOperationResult> UpdatePropertyAsync(
        Guid propertyId,
        string title,
        string address,
        decimal price,
        string priceCurrency,
        double area,
        int roomsCount,
        double latitude,
        double longitude,
        string type,
        string? ownerFullName,
        string? ownerEmail,
        string? ownerPhoneNumber,
        Guid? ownerClientId,
        IReadOnlyList<string> photoPaths,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Put, $"api/properties/{propertyId}", new
        {
            propertyId,
            title,
            address,
            price,
            priceCurrency,
            area,
            roomsCount,
            latitude,
            longitude,
            type,
            photoPaths,
            ownerFullName,
            ownerEmail,
            ownerPhoneNumber,
            ownerClientId
        }, cancellationToken);
    }

    public Task<ApiOperationResult> UpdatePriceAsync(
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

    public Task<ApiOperationResult> MarkAsSoldAsync(
        Guid propertyId,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Patch, $"api/properties/{propertyId}/mark-as-sold", null, cancellationToken);
    }

    public Task<ApiOperationResult> HideAsync(
        Guid propertyId,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Patch, $"api/properties/{propertyId}/hide", null, cancellationToken);
    }

    public Task<ApiOperationResult> ShowAsync(
        Guid propertyId,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Patch, $"api/properties/{propertyId}/show", null, cancellationToken);
    }

    public Task<ApiOperationResult> DeleteAsync(
        Guid propertyId,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Delete, $"api/properties/{propertyId}", null, cancellationToken);
    }

    public async Task<ApiClientResult<IReadOnlyList<RealEstateCriterionDefinitionViewModel>>> GetCriterionDefinitionsAsync(
        int limit = 500,
        bool includeHidden = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"api/property-criteria?limit={limit}&includeHidden={(includeHidden ? "true" : "false")}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<IReadOnlyList<RealEstateCriterionDefinitionViewModel>>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<List<CriterionDefinitionDto>>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<IReadOnlyList<RealEstateCriterionDefinitionViewModel>>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API вернул пустой список критериев."));
            }

            var mapped = payload
                .Select(x => new RealEstateCriterionDefinitionViewModel
                {
                    Id = x.Id,
                    Code = x.Code,
                    DisplayName = x.DisplayName,
                    ValueType = x.ValueType,
                    Category = x.Category,
                    Description = x.Description,
                    IsHidden = x.IsHidden,
                    Options = x.Options
                        .OrderBy(o => o.SortOrder)
                        .ThenBy(o => o.Label)
                        .Select(o => new RealEstateCriterionOptionViewModel
                        {
                            Value = o.Value,
                            Label = o.Label,
                            SortOrder = o.SortOrder
                        })
                        .ToList()
                })
                .ToList();

            return ApiClientResult<IReadOnlyList<RealEstateCriterionDefinitionViewModel>>.Success(mapped);
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<IReadOnlyList<RealEstateCriterionDefinitionViewModel>>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<IReadOnlyList<RealEstateCriterionDefinitionViewModel>>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public async Task<ApiClientResult<IReadOnlyList<RealEstateCriterionValueViewModel>>> GetPropertyCriteriaAsync(
        Guid propertyId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/properties/{propertyId}/criteria", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<IReadOnlyList<RealEstateCriterionValueViewModel>>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<List<PropertyCriterionValueDto>>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<IReadOnlyList<RealEstateCriterionValueViewModel>>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API вернул пустой список значений критериев."));
            }

            var mapped = payload
                .Select(x => new RealEstateCriterionValueViewModel
                {
                    CriterionDefinitionId = x.CriterionDefinitionId,
                    CriterionCode = x.CriterionCode,
                    DisplayName = x.CriterionDisplayName,
                    ValueType = x.CriterionValueType,
                    Category = x.Category,
                    RawValue = x.Value
                })
                .ToList();

            return ApiClientResult<IReadOnlyList<RealEstateCriterionValueViewModel>>.Success(mapped);
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<IReadOnlyList<RealEstateCriterionValueViewModel>>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<IReadOnlyList<RealEstateCriterionValueViewModel>>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public Task<ApiOperationResult> UpsertPropertyCriterionAsync(
        Guid propertyId,
        Guid criterionDefinitionId,
        string? value,
        IReadOnlyList<string>? values,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Put, $"api/properties/{propertyId}/criteria", new
        {
            criterionDefinitionId,
            value,
            values
        }, cancellationToken);
    }

    public Task<ApiOperationResult> DeletePropertyCriterionAsync(
        Guid propertyId,
        Guid criterionDefinitionId,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(
            _httpClient,
            HttpMethod.Delete,
            $"api/properties/{propertyId}/criteria/{criterionDefinitionId}",
            null,
            cancellationToken);
    }

    private static RealEstatePropertyCardViewModel MapPropertyCard(PropertyDto item)
    {
        return new RealEstatePropertyCardViewModel
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
            PhotoPaths = item.PhotoPaths ?? [],
            Criteria = (item.Criteria ?? [])
                .Select(x => new RealEstatePropertyCriterionShortViewModel
                {
                    Code = x.CriterionCode,
                    DisplayName = x.CriterionDisplayName,
                    Category = x.Category
                })
                .ToList(),
            MainPhotoPath = item.MainPhotoPath ?? item.PhotoPaths?.FirstOrDefault(),
            CreatedDate = item.CreatedDate
        };
    }

    private static RealEstatePropertyManagementViewModel MapPropertyManagement(PropertyManagementDto item)
    {
        return new RealEstatePropertyManagementViewModel
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
            OwnerClientId = item.OwnerClientId,
            PhotoPaths = item.PhotoPaths ?? [],
            MainPhotoPath = item.PhotoPaths?.FirstOrDefault(),
            CreatedDate = item.CreatedDate
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
        public string? OwnerFullName { get; set; }
        public string? OwnerEmail { get; set; }
        public string? OwnerPhoneNumber { get; set; }
        public string? MainPhotoPath { get; set; }
        public List<string>? PhotoPaths { get; set; }
        public List<PropertyCriterionShortDto>? Criteria { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    private sealed class PropertyManagementDto
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
        public Guid? OwnerClientId { get; set; }
        public List<string>? PhotoPaths { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    private sealed class CurrencyRateDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = "USD";
        public string Name { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public decimal RateToBase { get; set; }
        public bool IsActive { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    private sealed class CriterionDefinitionDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ValueType { get; set; } = "Undefined";
        public string? Category { get; set; }
        public string? Description { get; set; }
        public bool IsHidden { get; set; }
        public List<CriterionOptionDto> Options { get; set; } = [];
    }

    private sealed class CriterionOptionDto
    {
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int SortOrder { get; set; }
    }

    private sealed class PropertyCriterionValueDto
    {
        public Guid CriterionDefinitionId { get; set; }
        public string CriterionCode { get; set; } = string.Empty;
        public string CriterionDisplayName { get; set; } = string.Empty;
        public string CriterionValueType { get; set; } = "Undefined";
        public string? Category { get; set; }
        public string Value { get; set; } = string.Empty;
    }

    private sealed class PropertyCriterionShortDto
    {
        public string CriterionCode { get; set; } = string.Empty;
        public string CriterionDisplayName { get; set; } = string.Empty;
        public string? Category { get; set; }
    }
}
