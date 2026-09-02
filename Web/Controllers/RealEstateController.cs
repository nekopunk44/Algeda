using System.Text.Json;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.Models.Api;
using Web.Models.RealEstate;
using Web.Services;

namespace Web.Controllers;

[Authorize(Policy = "RequireRealtorOrAdmin")]
public class RealEstateController : Controller
{
    private const string SuccessKey = "RealEstateSuccess";
    private const string ErrorKey = "RealEstateError";
    private static readonly JsonSerializerOptions CriteriaJsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private readonly RealEstateApiClient _apiClient;
    private readonly PropertyPhotoApiClient _photoApiClient;

    public RealEstateController(
        RealEstateApiClient apiClient,
        PropertyPhotoApiClient photoApiClient)
    {
        _apiClient = apiClient;
        _photoApiClient = photoApiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        string? sellerSearch,
        string? criterionSearch,
        string? type,
        string? status,
        decimal? minPrice,
        decimal? maxPrice,
        string? priceCurrency,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 10, 100);

        var propertiesTask = _apiClient.GetPropertiesAsync(500, cancellationToken);
        var currenciesTask = _apiClient.GetCurrenciesAsync(200, includeInactive: false, cancellationToken);
        await Task.WhenAll(propertiesTask, currenciesTask);

        var propertiesResult = propertiesTask.Result;
        var currenciesResult = currenciesTask.Result;

        if (IsUnauthorized(propertiesResult.Error) || IsUnauthorized(currenciesResult.Error))
        {
            return RedirectToLoginCurrent();
        }

        var currencies = EnsureBaseCurrency(currenciesResult.Data ?? []);
        var safePriceCurrency = ResolvePriceCurrency(priceCurrency, currencies);

        var filters = new RealEstateFilterViewModel
        {
            Search = search,
            SellerSearch = sellerSearch,
            CriterionSearch = criterionSearch,
            Type = type,
            Status = status,
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            PriceCurrency = safePriceCurrency,
            Page = page,
            PageSize = pageSize
        };

        if (propertiesResult.Data is null)
        {
            return View(new RealEstateIndexViewModel
            {
                SuccessMessage = TempData[SuccessKey]?.ToString(),
                ApiError = propertiesResult.Error ?? currenciesResult.Error ?? GetTempErrorAsApiError(),
                Filters = filters,
                Currencies = currencies
            });
        }

        var all = propertiesResult.Data;
        var availableTypes = all
            .Select(x => x.Type)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();
        var availableStatuses = all
            .Select(x => x.Status)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        var filtered = all.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim();
            filtered = filtered.Where(x =>
                x.Title.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || x.Address.Contains(normalized, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(sellerSearch))
        {
            var normalized = sellerSearch.Trim();
            filtered = filtered.Where(x =>
                (!string.IsNullOrWhiteSpace(x.OwnerFullName)
                    && x.OwnerFullName.Contains(normalized, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(x.OwnerPhoneNumber)
                    && x.OwnerPhoneNumber.Contains(normalized, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(x.OwnerEmail)
                    && x.OwnerEmail.Contains(normalized, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(criterionSearch))
        {
            var normalized = criterionSearch.Trim();
            filtered = filtered.Where(x =>
                x.Criteria.Any(c =>
                    (!string.IsNullOrWhiteSpace(c.Code)
                        && c.Code.Contains(normalized, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrWhiteSpace(c.DisplayName)
                        && c.DisplayName.Contains(normalized, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrWhiteSpace(c.Category)
                        && c.Category.Contains(normalized, StringComparison.OrdinalIgnoreCase))));
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            filtered = filtered.Where(x =>
                string.Equals(x.Type, type, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filtered = filtered.Where(x =>
                string.Equals(x.Status, status, StringComparison.OrdinalIgnoreCase));
        }

        var priceRateToBase = GetCurrencyRateToBase(safePriceCurrency, currencies);
        var minPriceBase = ConvertPriceToBase(minPrice, priceRateToBase);
        var maxPriceBase = ConvertPriceToBase(maxPrice, priceRateToBase);

        if (minPriceBase.HasValue)
        {
            filtered = filtered.Where(x => x.Price >= minPriceBase.Value);
        }

        if (maxPriceBase.HasValue)
        {
            filtered = filtered.Where(x => x.Price <= maxPriceBase.Value);
        }

        var ordered = filtered
            .OrderByDescending(x => x.CreatedDate)
            .ThenBy(x => x.Title)
            .ToList();

        var totalCount = ordered.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Min(page, totalPages);

        var pageItems = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return View(new RealEstateIndexViewModel
        {
            SuccessMessage = TempData[SuccessKey]?.ToString(),
            ApiError = GetTempErrorAsApiError() ?? currenciesResult.Error,
            Filters = new RealEstateFilterViewModel
            {
                Search = filters.Search,
                SellerSearch = filters.SellerSearch,
                CriterionSearch = filters.CriterionSearch,
                Type = filters.Type,
                Status = filters.Status,
                MinPrice = filters.MinPrice,
                MaxPrice = filters.MaxPrice,
                PriceCurrency = filters.PriceCurrency,
                Page = page,
                PageSize = filters.PageSize
            },
            Items = pageItems,
            AvailableTypes = availableTypes,
            AvailableStatuses = availableStatuses,
            Currencies = currencies,
            TotalCount = totalCount,
            TotalPages = totalPages
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken = default)
    {
        var definitionsTask = _apiClient.GetCriterionDefinitionsAsync(
            500,
            includeHidden: true,
            cancellationToken);
        var currenciesTask = _apiClient.GetCurrenciesAsync(200, includeInactive: false, cancellationToken);
        await Task.WhenAll(definitionsTask, currenciesTask);

        var definitionsResult = definitionsTask.Result;
        var currenciesResult = currenciesTask.Result;

        if (IsUnauthorized(definitionsResult.Error) || IsUnauthorized(currenciesResult.Error))
        {
            return RedirectToLoginCurrent();
        }

        return View(new RealEstateCreateViewModel
        {
            ApiError = definitionsResult.Error ?? currenciesResult.Error ?? GetTempErrorAsApiError(),
            CriterionDefinitions = definitionsResult.Data ?? [],
            Currencies = currenciesResult.Data ?? []
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        string title,
        string address,
        decimal price,
        string priceCurrency,
        double area,
        int roomsCount,
        string latitude,
        string longitude,
        string type,
        string ownerFullName,
        string ownerEmail,
        string ownerPhoneNumber,
        Guid? ownerClientId,
        string? criteriaJson,
        List<IFormFile>? photos,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title)
            || string.IsNullOrWhiteSpace(address)
            || string.IsNullOrWhiteSpace(type)
            || string.IsNullOrWhiteSpace(ownerFullName)
            || string.IsNullOrWhiteSpace(ownerEmail)
            || string.IsNullOrWhiteSpace(ownerPhoneNumber))
        {
            TempData[ErrorKey] = "Заполните обязательные поля объекта.";
            return RedirectToAction(nameof(Create));
        }

        if (!TryParseCoordinates(latitude, longitude, out var parsedLatitude, out var parsedLongitude))
        {
            TempData[ErrorKey] = "Проверьте координаты объекта. Широта должна быть от -90 до 90, долгота от -180 до 180.";
            return RedirectToAction(nameof(Create));
        }

        var savedPhotosResult = await SaveUploadedPhotosAsync(photos, cancellationToken);
        if (!savedPhotosResult.IsSuccess)
        {
            TempData[ErrorKey] = savedPhotosResult.Error?.Message ?? "Не удалось загрузить фотографии.";
            return RedirectToAction(nameof(Create));
        }

        var savedPhotos = savedPhotosResult.Data ?? [];
        if (savedPhotos.Count > 20)
        {
            TempData[ErrorKey] = "Можно загрузить не более 20 фотографий.";
            return RedirectToAction(nameof(Create));
        }

        var createResult = await _apiClient.CreatePropertyAsync(
            title.Trim(),
            address.Trim(),
            price,
            priceCurrency,
            area,
            roomsCount,
            parsedLatitude,
            parsedLongitude,
            type.Trim(),
            ownerFullName?.Trim(),
            ownerEmail?.Trim(),
            ownerPhoneNumber?.Trim(),
            ownerClientId,
            savedPhotos,
            cancellationToken);

        if (!createResult.IsSuccess || createResult.Data == Guid.Empty)
        {
            TempData[ErrorKey] = createResult.Error?.Message ?? "Не удалось создать объект.";
            return RedirectToAction(nameof(Create));
        }

        var propertyId = createResult.Data;
        var criteriaResult = await UpsertCriteriaAsync(
            propertyId,
            ParseCriteria(criteriaJson),
            cancellationToken);
        if (!criteriaResult.IsSuccess)
        {
            TempData[ErrorKey] = criteriaResult.Error?.Message ?? "Объект создан, но не удалось сохранить часть критериев.";
            return RedirectToAction(nameof(Edit), new { id = propertyId });
        }

        TempData[SuccessKey] = "Объект недвижимости создан.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken = default)
    {
        var propertyTask = _apiClient.GetPropertyForManagementAsync(id, cancellationToken);
        var definitionsTask = _apiClient.GetCriterionDefinitionsAsync(500, includeHidden: true, cancellationToken);
        var valuesTask = _apiClient.GetPropertyCriteriaAsync(id, cancellationToken);
        var currenciesTask = _apiClient.GetCurrenciesAsync(200, includeInactive: false, cancellationToken);
        await Task.WhenAll(propertyTask, definitionsTask, valuesTask, currenciesTask);

        if (IsUnauthorized(propertyTask.Result.Error)
            || IsUnauthorized(definitionsTask.Result.Error)
            || IsUnauthorized(valuesTask.Result.Error)
            || IsUnauthorized(currenciesTask.Result.Error))
        {
            return RedirectToLoginCurrent();
        }

        var propertyResult = propertyTask.Result;
        var definitionsResult = definitionsTask.Result;
        var valuesResult = valuesTask.Result;
        var currenciesResult = currenciesTask.Result;

        var apiError = propertyResult.Error
            ?? definitionsResult.Error
            ?? valuesResult.Error
            ?? currenciesResult.Error
            ?? GetTempErrorAsApiError();

        return View(new RealEstateEditViewModel
        {
            SuccessMessage = TempData[SuccessKey]?.ToString(),
            ApiError = apiError,
            Property = propertyResult.Data,
            CriterionDefinitions = definitionsResult.Data ?? [],
            CurrentCriteria = valuesResult.Data ?? [],
            Currencies = currenciesResult.Data ?? []
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(
        Guid id,
        string title,
        string address,
        decimal price,
        string priceCurrency,
        double area,
        int roomsCount,
        string latitude,
        string longitude,
        string type,
        string ownerFullName,
        string ownerEmail,
        string ownerPhoneNumber,
        Guid? ownerClientId,
        string? criteriaJson,
        List<string>? retainPhotoPaths,
        List<IFormFile>? newPhotos,
        CancellationToken cancellationToken = default)
    {
        var uploadedPathsResult = await SaveUploadedPhotosAsync(newPhotos, cancellationToken);
        if (!uploadedPathsResult.IsSuccess)
        {
            TempData[ErrorKey] = uploadedPathsResult.Error?.Message ?? "Не удалось загрузить фотографии.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var uploadedPaths = uploadedPathsResult.Data ?? [];
        var finalPhotoPaths = (retainPhotoPaths ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Concat(uploadedPaths)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!TryParseCoordinates(latitude, longitude, out var parsedLatitude, out var parsedLongitude))
        {
            TempData[ErrorKey] = "Проверьте координаты объекта. Широта должна быть от -90 до 90, долгота от -180 до 180.";
            return RedirectToAction(nameof(Edit), new { id });
        }


        if (finalPhotoPaths.Count > 20)
        {
            TempData[ErrorKey] = "Можно загрузить не более 20 фотографий.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var result = await _apiClient.UpdatePropertyAsync(
            id,
            title,
            address,
            price,
            priceCurrency,
            area,
            roomsCount,
            parsedLatitude,
            parsedLongitude,
            type,
            ownerFullName,
            ownerEmail,
            ownerPhoneNumber,
            ownerClientId,
            finalPhotoPaths,
            cancellationToken);

        if (!result.IsSuccess)
        {
            SetOperationResult(result, "Карточка объекта обновлена.");
            return RedirectToAction(nameof(Edit), new { id });
        }

        var criteria = ParseCriteria(criteriaJson);
        if (criteria.Count > 0)
        {
            var criteriaResult = await UpsertCriteriaAsync(id, criteria, cancellationToken);
            if (!criteriaResult.IsSuccess)
            {
                TempData[ErrorKey] = criteriaResult.Error?.Message
                    ?? "Карточка объекта обновлена, но не удалось сохранить критерии.";
                return RedirectToAction(nameof(Edit), new { id });
            }
        }

        TempData[SuccessKey] = "Карточка объекта обновлена.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePrice(
        Guid propertyId,
        decimal newPrice,
        CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.UpdatePriceAsync(propertyId, newPrice, cancellationToken);
        SetOperationResult(result, "Цена объекта обновлена.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsSold(
        Guid propertyId,
        CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.MarkAsSoldAsync(propertyId, cancellationToken);
        SetOperationResult(result, "Статус объекта изменен на «Продан».");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Hide(
        Guid propertyId,
        CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.HideAsync(propertyId, cancellationToken);
        SetOperationResult(result, "Объект скрыт из общего списка.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Show(
        Guid propertyId,
        CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.ShowAsync(propertyId, cancellationToken);
        SetOperationResult(result, "Объект снова доступен в общем списке.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(
        Guid propertyId,
        CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.DeleteAsync(propertyId, cancellationToken);
        SetOperationResult(result, "Объект удален.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpsertCriterion(
        Guid id,
        Guid criterionDefinitionId,
        string? value,
        List<string>? values,
        CancellationToken cancellationToken = default)
    {
        var cleanedValues = values?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = await _apiClient.UpsertPropertyCriterionAsync(
            id,
            criterionDefinitionId,
            string.IsNullOrWhiteSpace(value) ? null : value.Trim(),
            cleanedValues,
            cancellationToken);

        SetOperationResult(result, "Значение критерия сохранено.");
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCriterion(
        Guid id,
        Guid criterionDefinitionId,
        CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.DeletePropertyCriterionAsync(id, criterionDefinitionId, cancellationToken);
        SetOperationResult(result, "Критерий удален из объекта.");
        return RedirectToAction(nameof(Edit), new { id });
    }

    private ApiErrorViewModel? GetTempErrorAsApiError()
    {
        var message = TempData[ErrorKey]?.ToString();
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        return ApiErrorFactory.Create(StatusCodes.Status400BadRequest, message);
    }

    private void SetOperationResult(ApiOperationResult result, string successMessage)
    {
        if (result.IsSuccess)
        {
            TempData[SuccessKey] = successMessage;
            return;
        }

        TempData[ErrorKey] = result.Error?.Message ?? "Не удалось выполнить операцию.";
    }

    private static bool IsUnauthorized(ApiErrorViewModel? error)
    {
        return error?.StatusCode == StatusCodes.Status401Unauthorized;
    }

    private IActionResult RedirectToLoginCurrent()
    {
        var returnUrl = $"{Request.Path}{Request.QueryString}";
        return RedirectToAction("Login", "Auth", new { returnUrl });
    }

    private async Task<ApiClientResult<IReadOnlyList<string>>> SaveUploadedPhotosAsync(
        IEnumerable<IFormFile>? photos,
        CancellationToken cancellationToken)
    {
        return await _photoApiClient.UploadPhotosAsync(photos, cancellationToken);
    }


    private static bool TryParseCoordinates(
        string? rawLatitude,
        string? rawLongitude,
        out double latitude,
        out double longitude)
    {
        latitude = 0;
        longitude = 0;

        if (!TryParseCoordinate(rawLatitude, out latitude)
            || !TryParseCoordinate(rawLongitude, out longitude))
        {
            return false;
        }

        return latitude >= -90d
            && latitude <= 90d
            && longitude >= -180d
            && longitude <= 180d;
    }

    private static bool TryParseCoordinate(string? rawValue, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        var normalized = rawValue.Trim();
        if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        normalized = normalized.Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
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

    private static string ResolvePriceCurrency(
        string? priceCurrency,
        IReadOnlyList<CurrencyRateViewModel> currencies)
    {
        var normalized = string.IsNullOrWhiteSpace(priceCurrency)
            ? "USD"
            : priceCurrency.Trim().ToUpperInvariant();

        return currencies.Any(x => string.Equals(x.Code, normalized, StringComparison.OrdinalIgnoreCase))
            ? normalized
            : "USD";
    }

    private static decimal GetCurrencyRateToBase(
        string currencyCode,
        IReadOnlyList<CurrencyRateViewModel> currencies)
    {
        var rate = currencies
            .FirstOrDefault(x => string.Equals(x.Code, currencyCode, StringComparison.OrdinalIgnoreCase))
            ?.RateToBase ?? 1m;

        return rate > 0 ? rate : 1m;
    }

    private static decimal? ConvertPriceToBase(decimal? amount, decimal rateToBase)
    {
        if (!amount.HasValue)
        {
            return null;
        }

        return decimal.Round(amount.Value * rateToBase, 2, MidpointRounding.AwayFromZero);
    }

    private async Task<ApiOperationResult> UpsertCriteriaAsync(
        Guid propertyId,
        IReadOnlyList<RealEstateCriterionSubmitModel> criteria,
        CancellationToken cancellationToken)
    {
        foreach (var criterion in criteria)
        {
            var upsertResult = await _apiClient.UpsertPropertyCriterionAsync(
                propertyId,
                criterion.CriterionDefinitionId,
                string.IsNullOrWhiteSpace(criterion.Value) ? null : criterion.Value.Trim(),
                criterion.Values?
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                cancellationToken);

            if (!upsertResult.IsSuccess)
                return upsertResult;
        }

        return ApiOperationResult.Success();
    }

    private static List<RealEstateCriterionSubmitModel> ParseCriteria(string? criteriaJson)
    {
        if (string.IsNullOrWhiteSpace(criteriaJson))
            return [];

        try
        {
            var parsed = JsonSerializer.Deserialize<List<RealEstateCriterionSubmitModel>>(
                criteriaJson,
                CriteriaJsonSerializerOptions);
            if (parsed is null)
                return [];

            return parsed
                .Where(x => x.CriterionDefinitionId != Guid.Empty)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
