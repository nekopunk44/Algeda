using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.Models.Api;
using Web.Models.Dashboard;
using Web.Models.RealEstate;
using Web.Services;

namespace Web.Controllers;

[Authorize(Policy = "RequireAnyAuthorized")]
public class MatchingController : Controller
{
    private const string SuccessKey = "DashboardSuccess";
    private const string ErrorKey = "DashboardErrorMessage";

    private const double FallbackLatitude = 47.0105;
    private const double FallbackLongitude = 28.8638;
    private const double FallbackRadiusMeters = 120000;
    private const double LegacyPriceWeight = 0.5;
    private const double LegacyAreaWeight = 0.5;

    private static readonly HashSet<string> AllowedPropertyTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Apartment",
        "House",
        "Commercial",
        "Land"
    };

    private readonly ClientDashboardApiClient _apiClient;
    private readonly RealEstateApiClient _realEstateApiClient;

    public MatchingController(
        ClientDashboardApiClient apiClient,
        RealEstateApiClient realEstateApiClient)
    {
        _apiClient = apiClient;
        _realEstateApiClient = realEstateApiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        bool run = false,
        int matchLimit = 20,
        string sortBy = "score",
        CancellationToken cancellationToken = default)
    {
        var canManageRequirement = User.IsInRole("Client");
        var safeLimit = Math.Clamp(matchLimit, 1, 100);
        var safeSort = NormalizeSort(sortBy);

        ApiErrorViewModel? apiError = GetTempApiError();
        var successMessage = TempData[SuccessKey]?.ToString();

        var criteriaDefinitions = Array.Empty<DashboardCriterionDefinitionViewModel>();
        var matchResults = Array.Empty<DashboardMatchResultViewModel>();
        var notificationHistory = Array.Empty<DashboardNotificationHistoryItemViewModel>();
        DashboardRequirementViewModel? activeRequirement = null;
        var currenciesResult = await _realEstateApiClient.GetCurrenciesAsync(200, includeInactive: false, cancellationToken);
        if (currenciesResult.Error?.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return RedirectToAuthGate();
        }

        var currencies = EnsureBaseCurrency(currenciesResult.Data ?? []);
        apiError ??= currenciesResult.Error;

        var criteriaResult = await _apiClient.GetCriteriaDefinitionsAsync(
            limit: 300,
            includeHidden: false,
            cancellationToken: cancellationToken);

        if (criteriaResult.Error?.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return RedirectToAuthGate();
        }

        if (criteriaResult.Data is not null)
        {
            criteriaDefinitions = [.. criteriaResult.Data];
        }
        else
        {
            apiError ??= criteriaResult.Error;
        }

        if (canManageRequirement)
        {
            var requirementResult = await _apiClient.GetMyActiveRequirementAsync(cancellationToken);
            if (requirementResult.Error?.StatusCode == StatusCodes.Status401Unauthorized)
            {
                return RedirectToAuthGate();
            }

            if (requirementResult.Data is not null)
            {
                activeRequirement = requirementResult.Data;
            }
            else if (requirementResult.Error?.StatusCode is not StatusCodes.Status404NotFound)
            {
                apiError ??= requirementResult.Error;
            }

            if (activeRequirement is not null)
            {
                var historyResult = await _apiClient.GetRequirementNotificationHistoryAsync(
                    activeRequirement.Id,
                    limit: 20,
                    cancellationToken: cancellationToken);

                if (historyResult.Error?.StatusCode == StatusCodes.Status401Unauthorized)
                {
                    return RedirectToAuthGate();
                }

                if (historyResult.Data is not null)
                {
                    notificationHistory = [.. historyResult.Data];
                }
                else if (historyResult.Error?.StatusCode is not StatusCodes.Status404NotFound)
                {
                    apiError ??= historyResult.Error;
                }

                if (run)
                {
                    var matchesResult = await _apiClient.GetMatchesAsync(activeRequirement.Id, safeLimit, cancellationToken);
                    if (matchesResult.Error?.StatusCode == StatusCodes.Status401Unauthorized)
                    {
                        return RedirectToAuthGate();
                    }

                    if (matchesResult.Data is not null)
                    {
                        matchResults = [.. SortMatches(matchesResult.Data, safeSort)];
                    }
                    else
                    {
                        apiError ??= matchesResult.Error;
                    }
                }
            }
        }

        return View(new ClientDashboardViewModel
        {
            SuccessMessage = successMessage,
            ApiError = apiError,
            MatchResults = matchResults,
            ActiveRequirement = activeRequirement,
            RequirementId = activeRequirement?.Id,
            MatchLimit = safeLimit,
            SortBy = safeSort,
            RunAttempted = run,
            CanManageRequirement = canManageRequirement,
            NotificationHistory = notificationHistory,
            CriterionDefinitions = criteriaDefinitions,
            Currencies = currencies
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> SaveRequirement(
        SaveRequirementInputModel input,
        bool runAfterSave = false,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values
                .SelectMany(x => x.Errors)
                .Select(x => x.ErrorMessage)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

            TempData[ErrorKey] = firstError ?? "Некорректный формат введенных данных.";
            return RedirectToAction(nameof(Index), new { matchLimit = input.MatchLimit, sortBy = input.SortBy });
        }

        var validationError = ValidateRequirementInput(input);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            TempData[ErrorKey] = validationError;
            return RedirectToAction(nameof(Index), new { matchLimit = input.MatchLimit, sortBy = input.SortBy });
        }

        var activeRequirementResult = await _apiClient.GetMyActiveRequirementAsync(cancellationToken);
        if (activeRequirementResult.Error?.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return RedirectToAuthGate();
        }

        if (activeRequirementResult.Error is not null
            && activeRequirementResult.Error.StatusCode != StatusCodes.Status404NotFound)
        {
            TempData[ErrorKey] = activeRequirementResult.Error.Message;
            return RedirectToAction(nameof(Index), new { matchLimit = input.MatchLimit, sortBy = input.SortBy });
        }

        var normalizedDesiredTypes = NormalizeDesiredTypes(input.DesiredTypes);
        var currenciesResult = await _realEstateApiClient.GetCurrenciesAsync(200, includeInactive: false, cancellationToken);
        if (currenciesResult.Error?.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return RedirectToAuthGate();
        }

        if (currenciesResult.Error is not null)
        {
            TempData[ErrorKey] = currenciesResult.Error.Message;
            return RedirectToAction(nameof(Index), new { matchLimit = input.MatchLimit, sortBy = input.SortBy });
        }

        var currencies = EnsureBaseCurrency(currenciesResult.Data ?? []);
        var priceRange = ConvertPriceRangeToBase(input, currencies);
        if (priceRange.Error is not null)
        {
            TempData[ErrorKey] = priceRange.Error;
            return RedirectToAction(nameof(Index), new { matchLimit = input.MatchLimit, sortBy = input.SortBy });
        }

        var legacyDesiredType = normalizedDesiredTypes.FirstOrDefault() ?? "Undefined";
        var hasValidAreaPoint = HasValidAreaPoint(input);
        var effectiveIgnoreArea = input.IgnoreArea || !hasValidAreaPoint;
        var (latitude, longitude, radiusMeters) = ResolveArea(input, effectiveIgnoreArea);
        var minMatchRatio = Math.Clamp(input.MinMatchPercentage / 100d, 0.01d, 1d);
        var criteriaPayload = BuildCriteriaPayload(input.Criteria);

        ApiOperationResult operationResult;
        if (activeRequirementResult.Data is null)
        {
            operationResult = await _apiClient.CreateMyRequirementAsync(
                legacyDesiredType,
                normalizedDesiredTypes,
                latitude,
                longitude,
                radiusMeters,
                effectiveIgnoreArea,
                priceRange.MinPriceBase,
                priceRange.MaxPriceBase,
                input.MinArea,
                input.MaxArea,
                input.AddressQuery?.Trim(),
                minMatchRatio,
                LegacyPriceWeight,
                LegacyAreaWeight,
                criteriaPayload,
                cancellationToken);
        }
        else
        {
            operationResult = await _apiClient.UpdateMyRequirementAsync(
                activeRequirementResult.Data.Id,
                legacyDesiredType,
                normalizedDesiredTypes,
                latitude,
                longitude,
                radiusMeters,
                effectiveIgnoreArea,
                priceRange.MinPriceBase,
                priceRange.MaxPriceBase,
                input.MinArea,
                input.MaxArea,
                input.AddressQuery?.Trim(),
                minMatchRatio,
                LegacyPriceWeight,
                LegacyAreaWeight,
                criteriaPayload,
                cancellationToken);
        }

        if (operationResult.IsSuccess)
        {
            TempData[SuccessKey] = activeRequirementResult.Data is null
                ? "Подбор сохранен."
                : "Подбор обновлен.";

            return RedirectToAction(nameof(Index), new
            {
                run = runAfterSave,
                matchLimit = input.MatchLimit,
                sortBy = input.SortBy
            });
        }

        TempData[ErrorKey] = operationResult.Error?.Message ?? "Не удалось сохранить подбор.";
        return RedirectToAction(nameof(Index), new { matchLimit = input.MatchLimit, sortBy = input.SortBy });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Client")]
    public IActionResult RunMatching(int matchLimit = 20, string sortBy = "score")
    {
        return RedirectToAction(nameof(Index), new
        {
            run = true,
            matchLimit,
            sortBy
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "RequireRealtorOrAdmin")]
    public async Task<IActionResult> RunPreview(
        SaveRequirementInputModel input,
        CancellationToken cancellationToken = default)
    {
        var safeSort = NormalizeSort(input.SortBy);
        var safeLimit = Math.Clamp(input.MatchLimit, 1, 100);

        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values
                .SelectMany(x => x.Errors)
                .Select(x => x.ErrorMessage)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

            TempData[ErrorKey] = firstError ?? "Некорректный формат введенных данных.";
            return RedirectToAction(nameof(Index), new { matchLimit = safeLimit, sortBy = safeSort });
        }

        var validationError = ValidateRequirementInput(input);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            TempData[ErrorKey] = validationError;
            return RedirectToAction(nameof(Index), new { matchLimit = safeLimit, sortBy = safeSort });
        }

        var criteriaResult = await _apiClient.GetCriteriaDefinitionsAsync(
            limit: 300,
            includeHidden: false,
            cancellationToken: cancellationToken);
        var currenciesResult = await _realEstateApiClient.GetCurrenciesAsync(200, includeInactive: false, cancellationToken);

        if (criteriaResult.Error?.StatusCode == StatusCodes.Status401Unauthorized
            || currenciesResult.Error?.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return RedirectToAuthGate();
        }

        var apiError = GetTempApiError() ?? criteriaResult.Error ?? currenciesResult.Error;
        var criteriaDefinitions = criteriaResult.Data ?? [];
        var currencies = EnsureBaseCurrency(currenciesResult.Data ?? []);
        var priceRange = ConvertPriceRangeToBase(input, currencies);
        if (priceRange.Error is not null)
        {
            TempData[ErrorKey] = priceRange.Error;
            return RedirectToAction(nameof(Index), new { matchLimit = safeLimit, sortBy = safeSort });
        }

        var normalizedDesiredTypes = NormalizeDesiredTypes(input.DesiredTypes);
        var desiredType = normalizedDesiredTypes.FirstOrDefault() ?? "Undefined";
        var hasValidAreaPoint = HasValidAreaPoint(input);
        var effectiveIgnoreArea = input.IgnoreArea || !hasValidAreaPoint;
        var (latitude, longitude, radiusMeters) = ResolveArea(input, effectiveIgnoreArea);
        var minMatchRatio = Math.Clamp(input.MinMatchPercentage / 100d, 0.01d, 1d);
        var criteriaPayload = BuildCriteriaPayload(input.Criteria);

        var previewResult = await _apiClient.PreviewMatchesAsync(
            desiredType,
            normalizedDesiredTypes,
            latitude,
            longitude,
            radiusMeters,
            effectiveIgnoreArea,
            priceRange.MinPriceBase,
            priceRange.MaxPriceBase,
            input.MinArea,
            input.MaxArea,
            input.AddressQuery?.Trim(),
            minMatchRatio,
            criteriaPayload,
            safeLimit,
            cancellationToken);

        if (previewResult.Error?.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return RedirectToAuthGate();
        }

        if (previewResult.Data is null)
        {
            apiError ??= previewResult.Error;
        }

        var draftRequirement = BuildDraftRequirement(
            normalizedDesiredTypes,
            latitude,
            longitude,
            radiusMeters,
            effectiveIgnoreArea,
            minMatchRatio,
            input);

        return View("Index", new ClientDashboardViewModel
        {
            ApiError = apiError,
            MatchResults = previewResult.Data is null ? [] : [.. SortMatches(previewResult.Data, safeSort)],
            ActiveRequirement = draftRequirement,
            RequirementId = null,
            MatchLimit = safeLimit,
            SortBy = safeSort,
            RunAttempted = true,
            CanManageRequirement = false,
            NotificationHistory = [],
            CriterionDefinitions = criteriaDefinitions,
            Currencies = currencies
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> Subscribe(
        Guid requirementId,
        int topMatchesLimit = 5,
        int matchLimit = 20,
        string sortBy = "score",
        CancellationToken cancellationToken = default)
    {
        if (requirementId == Guid.Empty)
        {
            TempData[ErrorKey] = "Не найден активный подбор для подписки.";
            return RedirectToAction(nameof(Index), new { matchLimit, sortBy });
        }

        var result = await _apiClient.SubscribeRequirementAsync(requirementId, topMatchesLimit, cancellationToken);
        if (result.Error?.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return RedirectToAuthGate();
        }

        if (result.Error is not null)
        {
            TempData[ErrorKey] = result.Error.Message;
            return RedirectToAction(nameof(Index), new { matchLimit, sortBy });
        }

        TempData[SuccessKey] = result.Data > 0
            ? $"Подписка активирована. Отправлено писем: {result.Data}."
            : "Подписка активирована. Новых совпадений для отправки пока нет.";

        return RedirectToAction(nameof(Index), new { run = true, matchLimit, sortBy });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> DeleteRequirement(
        Guid requirementId,
        int matchLimit = 20,
        string sortBy = "score",
        CancellationToken cancellationToken = default)
    {
        if (requirementId == Guid.Empty)
        {
            TempData[ErrorKey] = "Не найден активный подбор для удаления.";
            return RedirectToAction(nameof(Index), new { matchLimit, sortBy });
        }

        var result = await _apiClient.DeleteRequirementAsync(requirementId, cancellationToken);
        if (result.Error?.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return RedirectToAuthGate();
        }

        if (result.IsSuccess)
        {
            TempData[SuccessKey] = "Подбор удален.";
        }
        else
        {
            TempData[ErrorKey] = result.Error?.Message ?? "Не удалось удалить подбор.";
        }

        return RedirectToAction(nameof(Index), new { matchLimit, sortBy });
    }

    private static List<string> NormalizeDesiredTypes(List<string>? desiredTypes)
    {
        if (desiredTypes is null || desiredTypes.Count == 0)
        {
            return [];
        }

        return desiredTypes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Where(x => AllowedPropertyTypes.Contains(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static (double Latitude, double Longitude, double RadiusMeters) ResolveArea(
        SaveRequirementInputModel input,
        bool ignoreArea)
    {
        var latitude = input.Latitude ?? FallbackLatitude;
        var longitude = input.Longitude ?? FallbackLongitude;

        if (ignoreArea)
        {
            return (latitude, longitude, FallbackRadiusMeters);
        }

        var radiusKm = input.SearchRadiusKm <= 0 ? 5 : input.SearchRadiusKm;
        var radiusMeters = radiusKm * 1000d;

        return (latitude, longitude, radiusMeters);
    }

    private static string? ValidateRequirementInput(SaveRequirementInputModel input)
    {
        if (input.MinPrice < 0)
        {
            return "Минимальная цена не может быть отрицательной.";
        }

        if (input.MaxPrice <= 0)
        {
            return "Максимальная цена должна быть больше нуля.";
        }

        if (input.MaxPrice < input.MinPrice)
        {
            return "Проверьте диапазон цены.";
        }

        if (input.MinArea <= 0)
        {
            return "Минимальная площадь должна быть больше нуля.";
        }

        if (input.MaxArea.HasValue && input.MaxArea.Value < input.MinArea)
        {
            return "Максимальная площадь не может быть меньше минимальной.";
        }

        if (!string.IsNullOrWhiteSpace(input.AddressQuery) && input.AddressQuery.Trim().Length > 500)
        {
            return "Строка адреса слишком длинная (максимум 500 символов).";
        }

        if (input.MinMatchPercentage <= 0 || input.MinMatchPercentage > 100)
        {
            return "Минимальное совпадение должно быть в диапазоне от 1 до 100.";
        }

        if (!input.IgnoreArea && HasAnyAreaPointValue(input))
        {
            if (input.Latitude is < -90 or > 90)
            {
                return "Выберите корректную точку на карте.";
            }

            if (input.Longitude is < -180 or > 180)
            {
                return "Выберите корректную точку на карте.";
            }
        }

        if (!input.IgnoreArea && HasValidAreaPoint(input))
        {
            if (input.SearchRadiusKm < 0.1)
            {
                return "Радиус поиска должен быть не меньше 0.1 км.";
            }
        }

        return null;
    }

    private static bool HasAnyAreaPointValue(SaveRequirementInputModel input)
    {
        return input.Latitude.HasValue || input.Longitude.HasValue;
    }

    private static bool HasValidAreaPoint(SaveRequirementInputModel input)
    {
        return input.Latitude.HasValue
               && input.Longitude.HasValue
               && input.Latitude is >= -90 and <= 90
               && input.Longitude is >= -180 and <= 180;
    }

    private static IReadOnlyList<ClientDashboardApiClient.RequirementCriterionPayload>? BuildCriteriaPayload(
        List<RequirementCriterionInputModel>? criteria)
    {
        if (criteria is null || criteria.Count == 0)
        {
            return null;
        }

        var result = new List<ClientDashboardApiClient.RequirementCriterionPayload>();

        foreach (var criterion in criteria)
        {
            if (criterion.CriterionDefinitionId == Guid.Empty)
            {
                continue;
            }

            var priority = NormalizePriority(criterion.Priority);
            if (priority is "Undefined")
            {
                continue;
            }

            var isMultiSelect = string.Equals(
                criterion.ValueType,
                "MultiSelect",
                StringComparison.OrdinalIgnoreCase);

            if (isMultiSelect)
            {
                var values = (criterion.Values ?? [])
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (values.Count == 0)
                {
                    continue;
                }

                result.Add(new ClientDashboardApiClient.RequirementCriterionPayload
                {
                    CriterionDefinitionId = criterion.CriterionDefinitionId,
                    Priority = priority,
                    Values = values
                });

                continue;
            }

            var value = criterion.Value?.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            result.Add(new ClientDashboardApiClient.RequirementCriterionPayload
            {
                CriterionDefinitionId = criterion.CriterionDefinitionId,
                Priority = priority,
                Value = value
            });
        }

        return result.Count == 0 ? null : result;
    }

    private static DashboardRequirementViewModel BuildDraftRequirement(
        IReadOnlyList<string> desiredTypes,
        double latitude,
        double longitude,
        double searchRadiusMeters,
        bool ignoreArea,
        double minMatchRatio,
        SaveRequirementInputModel input)
    {
        var criteria = (input.Criteria ?? [])
            .Where(x => x.CriterionDefinitionId != Guid.Empty)
            .Select(x => new DashboardRequirementCriterionViewModel
            {
                CriterionDefinitionId = x.CriterionDefinitionId,
                Priority = NormalizePriority(x.Priority),
                Value = string.IsNullOrWhiteSpace(x.Value) ? null : x.Value.Trim(),
                Values = (x.Values ?? [])
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Select(v => v.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            })
            .Where(x => x.Priority is "MustHave" or "Important" or "NiceToHave")
            .ToList();

        return new DashboardRequirementViewModel
        {
            Id = Guid.Empty,
            ClientId = Guid.Empty,
            DesiredType = desiredTypes.FirstOrDefault() ?? "Undefined",
            DesiredTypes = desiredTypes,
            Latitude = latitude,
            Longitude = longitude,
            SearchRadiusMeters = searchRadiusMeters,
            IgnoreArea = ignoreArea,
            MinPrice = input.MinPrice,
            MaxPrice = input.MaxPrice,
            PriceCurrency = NormalizePriceCurrency(input.PriceCurrency),
            MinArea = input.MinArea,
            MaxArea = input.MaxArea,
            AddressQuery = input.AddressQuery?.Trim(),
            MinMatchPercentage = minMatchRatio,
            PriceWeight = LegacyPriceWeight,
            AreaWeight = LegacyAreaWeight,
            IsActive = true,
            Criteria = criteria
        };
    }

    private static string NormalizePriority(string? priority)
    {
        return priority?.Trim() switch
        {
            "MustHave" => "MustHave",
            "Important" => "Important",
            "NiceToHave" => "NiceToHave",
            _ => "Undefined"
        };
    }

    private static IReadOnlyList<DashboardMatchResultViewModel> SortMatches(
        IReadOnlyList<DashboardMatchResultViewModel> source,
        string sortBy)
    {
        return sortBy switch
        {
            "distance" => [.. source.OrderBy(x => x.DistanceMeters).ThenByDescending(x => x.MatchScore)],
            "price" => [.. source.OrderBy(x => x.Price).ThenByDescending(x => x.MatchScore)],
            _ => [.. source.OrderByDescending(x => x.MatchScore).ThenBy(x => x.DistanceMeters)]
        };
    }

    private static string NormalizeSort(string? sortBy)
    {
        return sortBy?.Trim().ToLowerInvariant() switch
        {
            "distance" => "distance",
            "price" => "price",
            _ => "score"
        };
    }

    private static IReadOnlyList<CurrencyRateViewModel> EnsureBaseCurrency(
        IReadOnlyList<CurrencyRateViewModel> currencies)
    {
        if (currencies.Any(x => string.Equals(x.Code, "USD", StringComparison.OrdinalIgnoreCase)))
        {
            return currencies;
        }

        return
        [
            new CurrencyRateViewModel
            {
                Code = "USD",
                Name = "US Dollar",
                Symbol = "$",
                RateToBase = 1m,
                IsActive = true
            },
            .. currencies
        ];
    }

    private static string NormalizePriceCurrency(string? priceCurrency)
    {
        return string.IsNullOrWhiteSpace(priceCurrency)
            ? "USD"
            : priceCurrency.Trim().ToUpperInvariant();
    }

    private static (decimal MinPriceBase, decimal MaxPriceBase, string? Error) ConvertPriceRangeToBase(
        SaveRequirementInputModel input,
        IReadOnlyList<CurrencyRateViewModel> currencies)
    {
        var currencyCode = NormalizePriceCurrency(input.PriceCurrency);
        var currency = currencies.FirstOrDefault(x =>
            string.Equals(x.Code, currencyCode, StringComparison.OrdinalIgnoreCase));

        if (currency is null || currency.RateToBase <= 0)
        {
            return (0m, 0m, "Выберите корректную валюту цены.");
        }

        return (
            decimal.Round(input.MinPrice * currency.RateToBase, 2, MidpointRounding.AwayFromZero),
            decimal.Round(input.MaxPrice * currency.RateToBase, 2, MidpointRounding.AwayFromZero),
            null);
    }

    private ApiErrorViewModel? GetTempApiError()
    {
        var message = TempData[ErrorKey]?.ToString();
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        return ApiErrorFactory.Create(StatusCodes.Status400BadRequest, message);
    }

    private IActionResult RedirectToAuthGate()
    {
        var returnUrl = $"{Request.Path}{Request.QueryString}";
        return RedirectToAction("Login", "Auth", new
        {
            returnUrl,
            registrationHint = true
        });
    }

    public sealed class SaveRequirementInputModel
    {
        public List<string>? DesiredTypes { get; set; }

        public bool IgnoreArea { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        public double SearchRadiusKm { get; set; } = 5;

        public decimal MinPrice { get; set; } = 0m;

        public decimal MaxPrice { get; set; } = 200000m;

        public string PriceCurrency { get; set; } = "USD";

        public double MinArea { get; set; } = 30d;

        public double? MaxArea { get; set; }

        public string? AddressQuery { get; set; }

        public double MinMatchPercentage { get; set; } = 70;

        public int MatchLimit { get; set; } = 20;

        public string SortBy { get; set; } = "score";

        public List<RequirementCriterionInputModel>? Criteria { get; set; }
    }

    public sealed class RequirementCriterionInputModel
    {
        public Guid CriterionDefinitionId { get; set; }

        public string ValueType { get; set; } = "Undefined";

        public string Priority { get; set; } = "Undefined";

        public string? Value { get; set; }

        public List<string>? Values { get; set; }
    }
}
