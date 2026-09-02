using Web.Models.Api;
using Web.Models.Dashboard;
using System.Text.Json;

namespace Web.Services;

public class ClientDashboardApiClient
{
    private readonly HttpClient _httpClient;

    public ClientDashboardApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public sealed class RequirementCriterionPayload
    {
        public Guid CriterionDefinitionId { get; init; }

        public string Priority { get; init; } = "Undefined";

        public string? Value { get; init; }

        public IReadOnlyList<string>? Values { get; init; }
    }

    public async Task<ApiClientResult<IReadOnlyList<DashboardReviewViewModel>>> GetReviewsAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/reviews?limit={limit}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<IReadOnlyList<DashboardReviewViewModel>>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<List<ReviewDto>>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<IReadOnlyList<DashboardReviewViewModel>>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API returned an empty reviews payload."));
            }

            var mapped = payload.Select(item => new DashboardReviewViewModel
            {
                Id = item.Id,
                DealId = item.DealId,
                RealtorId = item.RealtorId,
                ClientId = item.ClientId,
                Score = item.Score,
                Comment = item.Comment,
                CreatedDate = item.CreatedDate
            }).ToList();

            return ApiClientResult<IReadOnlyList<DashboardReviewViewModel>>.Success(mapped);
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<IReadOnlyList<DashboardReviewViewModel>>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<IReadOnlyList<DashboardReviewViewModel>>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public Task<ApiOperationResult> CreateReviewAsync(
        Guid dealId,
        Guid realtorId,
        Guid clientId,
        int score,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Post, "api/reviews", new
        {
            dealId,
            realtorId,
            clientId,
            score,
            comment
        }, cancellationToken);
    }

    public Task<ApiOperationResult> CreateRequirementAsync(
        Guid clientId,
        string desiredType,
        IReadOnlyList<string>? desiredTypes,
        double latitude,
        double longitude,
        double searchRadiusMeters,
        bool ignoreArea,
        decimal minPrice,
        decimal maxPrice,
        double minArea,
        double? maxArea,
        string? addressQuery,
        double minMatchPercentage,
        double priceWeight,
        double areaWeight,
        IReadOnlyList<RequirementCriterionPayload>? criteria = null,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Post, "api/client-requirements", new
        {
            clientId,
            desiredType,
            desiredTypes,
            latitude,
            longitude,
            searchRadiusMeters,
            ignoreArea,
            minPrice,
            maxPrice,
            minArea,
            maxArea,
            addressQuery,
            minMatchPercentage,
            priceWeight,
            areaWeight,
            criteria
        }, cancellationToken);
    }

    public Task<ApiOperationResult> CreateMyRequirementAsync(
        string desiredType,
        IReadOnlyList<string>? desiredTypes,
        double latitude,
        double longitude,
        double searchRadiusMeters,
        bool ignoreArea,
        decimal minPrice,
        decimal maxPrice,
        double minArea,
        double? maxArea,
        string? addressQuery,
        double minMatchPercentage,
        double priceWeight,
        double areaWeight,
        IReadOnlyList<RequirementCriterionPayload>? criteria = null,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Post, "api/client-requirements/me", new
        {
            desiredType,
            desiredTypes,
            latitude,
            longitude,
            searchRadiusMeters,
            ignoreArea,
            minPrice,
            maxPrice,
            minArea,
            maxArea,
            addressQuery,
            minMatchPercentage,
            priceWeight,
            areaWeight,
            criteria
        }, cancellationToken);
    }

    public Task<ApiOperationResult> UpdateRequirementAsync(
        Guid requirementId,
        string desiredType,
        IReadOnlyList<string>? desiredTypes,
        double latitude,
        double longitude,
        double searchRadiusMeters,
        bool ignoreArea,
        decimal minPrice,
        decimal maxPrice,
        double minArea,
        double? maxArea,
        string? addressQuery,
        double minMatchPercentage,
        double priceWeight,
        double areaWeight,
        IReadOnlyList<RequirementCriterionPayload>? criteria = null,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Put, $"api/client-requirements/{requirementId}", new
        {
            id = requirementId,
            desiredType,
            desiredTypes,
            latitude,
            longitude,
            searchRadiusMeters,
            ignoreArea,
            minPrice,
            maxPrice,
            minArea,
            maxArea,
            addressQuery,
            minMatchPercentage,
            priceWeight,
            areaWeight,
            criteria
        }, cancellationToken);
    }

    public Task<ApiOperationResult> UpdateMyRequirementAsync(
        Guid requirementId,
        string desiredType,
        IReadOnlyList<string>? desiredTypes,
        double latitude,
        double longitude,
        double searchRadiusMeters,
        bool ignoreArea,
        decimal minPrice,
        decimal maxPrice,
        double minArea,
        double? maxArea,
        string? addressQuery,
        double minMatchPercentage,
        double priceWeight,
        double areaWeight,
        IReadOnlyList<RequirementCriterionPayload>? criteria = null,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Put, $"api/client-requirements/me/{requirementId}", new
        {
            desiredType,
            desiredTypes,
            latitude,
            longitude,
            searchRadiusMeters,
            ignoreArea,
            minPrice,
            maxPrice,
            minArea,
            maxArea,
            addressQuery,
            minMatchPercentage,
            priceWeight,
            areaWeight,
            criteria
        }, cancellationToken);
    }

    public Task<ApiOperationResult> DeleteRequirementAsync(Guid requirementId, CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Delete, $"api/client-requirements/{requirementId}", null, cancellationToken);
    }

    public async Task<ApiClientResult<int>> SubscribeRequirementAsync(
        Guid requirementId,
        int topMatchesLimit = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"api/client-requirements/{requirementId}/subscribe?topMatchesLimit={Math.Clamp(topMatchesLimit, 1, 20)}",
                null,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<int>.Failure(await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<SubscribeResponseDto>(
                ApiClientSupport.JsonOptions,
                cancellationToken);

            return payload is null
                ? ApiClientResult<int>.Failure(ApiClientSupport.BuildEmptyPayloadError("API returned an empty subscription response."))
                : ApiClientResult<int>.Success(payload.EmailsSent);
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

    public async Task<ApiClientResult<IReadOnlyList<DashboardCriterionDefinitionViewModel>>> GetCriteriaDefinitionsAsync(
        int limit = 200,
        bool includeHidden = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var includeHiddenValue = includeHidden ? "true" : "false";
            var response = await _httpClient.GetAsync(
                $"api/property-criteria?limit={limit}&includeHidden={includeHiddenValue}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<IReadOnlyList<DashboardCriterionDefinitionViewModel>>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<List<CriterionDefinitionDto>>(
                ApiClientSupport.JsonOptions,
                cancellationToken);

            if (payload is null)
            {
                return ApiClientResult<IReadOnlyList<DashboardCriterionDefinitionViewModel>>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API returned an empty criteria definitions payload."));
            }

            var mapped = payload.Select(item => new DashboardCriterionDefinitionViewModel
            {
                Id = item.Id,
                Code = item.Code,
                DisplayName = item.DisplayName,
                ValueType = item.ValueType,
                Category = item.Category,
                Description = item.Description,
                Options = (item.Options ?? [])
                    .OrderBy(option => option.SortOrder)
                    .Select(option => new DashboardCriterionOptionViewModel
                    {
                        Value = option.Value,
                        Label = option.Label,
                        SortOrder = option.SortOrder
                    }).ToList()
            }).ToList();

            return ApiClientResult<IReadOnlyList<DashboardCriterionDefinitionViewModel>>.Success(mapped);
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<IReadOnlyList<DashboardCriterionDefinitionViewModel>>.Failure(
                ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<IReadOnlyList<DashboardCriterionDefinitionViewModel>>.Failure(
                ApiClientSupport.BuildTimeoutApiError());
        }
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

            return ApiClientResult<DashboardRequirementViewModel>.Success(MapRequirement(payload));
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

    public async Task<ApiClientResult<DashboardRequirementViewModel>> GetMyActiveRequirementAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("api/client-requirements/me/active", cancellationToken);
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

            return ApiClientResult<DashboardRequirementViewModel>.Success(MapRequirement(payload));
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

    public async Task<ApiClientResult<IReadOnlyList<DashboardMatchResultViewModel>>> GetMatchesAsync(
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
                Latitude = item.Latitude,
                Longitude = item.Longitude,
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

    public async Task<ApiClientResult<IReadOnlyList<DashboardMatchResultViewModel>>> PreviewMatchesAsync(
        string desiredType,
        IReadOnlyList<string>? desiredTypes,
        double latitude,
        double longitude,
        double searchRadiusMeters,
        bool ignoreArea,
        decimal minPrice,
        decimal maxPrice,
        double minArea,
        double? maxArea,
        string? addressQuery,
        double minMatchPercentage,
        IReadOnlyList<RequirementCriterionPayload>? criteria,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/property-matching/preview")
            {
                Content = JsonContent.Create(new
                {
                    desiredType,
                    desiredTypes,
                    latitude,
                    longitude,
                    searchRadiusMeters,
                    ignoreArea,
                    minPrice,
                    maxPrice,
                    minArea,
                    maxArea,
                    addressQuery,
                    minMatchPercentage,
                    criteria,
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
                Latitude = item.Latitude,
                Longitude = item.Longitude,
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

    public async Task<ApiClientResult<DashboardHealthViewModel>> GetAutoMatchingHealthAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("api/health/auto-matching", cancellationToken);
            if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.ServiceUnavailable)
            {
                return ApiClientResult<DashboardHealthViewModel>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<AutoMatchingHealthDto>(
                ApiClientSupport.JsonOptions,
                cancellationToken);

            if (payload is null)
            {
                return ApiClientResult<DashboardHealthViewModel>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API returned an empty auto-matching health payload."));
            }

            var detailsParts = new List<string>();

            if (!string.IsNullOrWhiteSpace(payload.Worker))
            {
                detailsParts.Add($"Worker: {payload.Worker}");
            }

            if (payload.Enabled.HasValue)
            {
                detailsParts.Add($"Enabled: {(payload.Enabled.Value ? "yes" : "no")}");
            }

            if (payload.Iteration.HasValue)
            {
                detailsParts.Add($"Iteration: {payload.Iteration.Value}");
            }

            if (payload.LastEmailsSentCount.HasValue)
            {
                detailsParts.Add($"Last emails sent: {payload.LastEmailsSentCount.Value}");
            }

            if (!string.IsNullOrWhiteSpace(payload.LastError))
            {
                detailsParts.Add(payload.LastError);
            }

            return ApiClientResult<DashboardHealthViewModel>.Success(new DashboardHealthViewModel
            {
                Status = NormalizeHealthStatus(payload.Status),
                Alive = payload.Alive,
                TimestampUtc = payload.TimestampUtc,
                Details = detailsParts.Count == 0
                    ? "Дополнительные сведения отсутствуют."
                    : string.Join(" | ", detailsParts)
            });
        }
        catch (JsonException)
        {
            return ApiClientResult<DashboardHealthViewModel>.Failure(
                ApiClientSupport.BuildEmptyPayloadError("API вернул некорректный формат состояния автоподбора."));
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<DashboardHealthViewModel>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<DashboardHealthViewModel>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public async Task<ApiClientResult<IReadOnlyList<DashboardNotificationHistoryItemViewModel>>> GetRequirementNotificationHistoryAsync(
        Guid requirementId,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var safeLimit = Math.Clamp(limit, 1, 100);
            var response = await _httpClient.GetAsync(
                $"api/client-requirements/{requirementId}/notifications/history?limit={safeLimit}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<IReadOnlyList<DashboardNotificationHistoryItemViewModel>>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<List<RequirementNotificationHistoryDto>>(
                ApiClientSupport.JsonOptions,
                cancellationToken);

            if (payload is null)
            {
                return ApiClientResult<IReadOnlyList<DashboardNotificationHistoryItemViewModel>>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API returned an empty notification history payload."));
            }

            var mapped = payload.Select(item => new DashboardNotificationHistoryItemViewModel
            {
                PropertyId = item.PropertyId,
                Title = item.Title,
                Address = item.Address,
                Price = item.Price,
                Area = item.Area,
                SentAtUtc = item.SentAtUtc,
                NotificationType = item.NotificationType
            }).ToList();

            return ApiClientResult<IReadOnlyList<DashboardNotificationHistoryItemViewModel>>.Success(mapped);
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<IReadOnlyList<DashboardNotificationHistoryItemViewModel>>.Failure(
                ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<IReadOnlyList<DashboardNotificationHistoryItemViewModel>>.Failure(
                ApiClientSupport.BuildTimeoutApiError());
        }
    }

    private static DashboardRequirementViewModel MapRequirement(RequirementDto payload)
    {
        return new DashboardRequirementViewModel
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
            IsActive = payload.IsActive,
            Criteria = payload.Criteria?.Select(item => new DashboardRequirementCriterionViewModel
            {
                CriterionDefinitionId = item.CriterionDefinitionId,
                Priority = item.Priority,
                Value = item.Value,
                Values = item.Values ?? []
            }).ToList() ?? []
        };
    }

    private sealed class ReviewDto
    {
        public Guid Id { get; set; }
        public Guid DealId { get; set; }
        public Guid RealtorId { get; set; }
        public Guid ClientId { get; set; }
        public int Score { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedDate { get; set; }
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
        public List<RequirementCriterionDto>? Criteria { get; set; }
    }

    private sealed class RequirementNotificationHistoryDto
    {
        public Guid PropertyId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public double Area { get; set; }

        public DateTime SentAtUtc { get; set; }

        public string NotificationType { get; set; } = string.Empty;
    }

    private sealed class SubscribeResponseDto
    {
        public Guid RequirementId { get; set; }

        public int EmailsSent { get; set; }
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
        public double Latitude { get; set; }
        public double Longitude { get; set; }
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

    private sealed class AutoMatchingHealthDto
    {
        public JsonElement Status { get; set; }

        public string? Worker { get; set; }

        public bool? Enabled { get; set; }

        public bool? Alive { get; set; }

        public int? Iteration { get; set; }

        public int? LastEmailsSentCount { get; set; }

        public string? LastError { get; set; }

        public DateTime? TimestampUtc { get; set; }
    }

    private static string NormalizeHealthStatus(JsonElement statusElement)
    {
        return statusElement.ValueKind switch
        {
            JsonValueKind.String => string.IsNullOrWhiteSpace(statusElement.GetString())
                ? "unknown"
                : statusElement.GetString()!,
            JsonValueKind.Number => statusElement.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => "unknown"
        };
    }

    private sealed class RequirementCriterionDto
    {
        public Guid CriterionDefinitionId { get; set; }

        public string Priority { get; set; } = "Undefined";

        public string? Value { get; set; }

        public List<string>? Values { get; set; }
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

        public List<CriterionOptionDto>? Options { get; set; }
    }

    private sealed class CriterionOptionDto
    {
        public string Value { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public int SortOrder { get; set; }
    }
}
