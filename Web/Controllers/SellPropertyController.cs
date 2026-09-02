using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.Models.Api;
using Web.Models.RealEstate;
using Web.Models.SellProperty;
using Web.Services;

namespace Web.Controllers;

[Authorize(Policy = "RequireAnyAuthorized")]
public class SellPropertyController : Controller
{
    private const string SuccessKey = "SellPropertySuccess";
    private const string ErrorKey = "SellPropertyError";
    private static readonly JsonSerializerOptions CriteriaJsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly DealRequestsApiClient _dealRequestsApiClient;
    private readonly RealEstateApiClient _realEstateApiClient;
    private readonly PropertyPhotoApiClient _photoApiClient;

    public SellPropertyController(
        DealRequestsApiClient dealRequestsApiClient,
        RealEstateApiClient realEstateApiClient,
        PropertyPhotoApiClient photoApiClient)
    {
        _dealRequestsApiClient = dealRequestsApiClient;
        _realEstateApiClient = realEstateApiClient;
        _photoApiClient = photoApiClient;
    }

    [HttpGet]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> Index(
        string? search = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var requestsResult = await _dealRequestsApiClient.GetMySaleRequestsAsync(500, search, status, cancellationToken);
        if (IsUnauthorized(requestsResult.Error))
        {
            return RedirectToLoginCurrent();
        }
        return View(new SellPropertyIndexViewModel
        {
            SuccessMessage = TempData[SuccessKey]?.ToString(),
            ApiError = GetTempApiError() ?? requestsResult.Error,
            Search = search,
            Status = status,
            Items = requestsResult.Data ?? []
        });
    }

    [HttpGet]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken = default)
    {
        var definitionsTask = _realEstateApiClient.GetCriterionDefinitionsAsync(
            500,
            includeHidden: false,
            cancellationToken);
        var currenciesTask = _realEstateApiClient.GetCurrenciesAsync(200, includeInactive: false, cancellationToken);
        await Task.WhenAll(definitionsTask, currenciesTask);

        var definitionsResult = definitionsTask.Result;
        var currenciesResult = currenciesTask.Result;

        if (IsUnauthorized(definitionsResult.Error) || IsUnauthorized(currenciesResult.Error))
        {
            return RedirectToLoginCurrent();
        }

        return View("Form", new SellPropertyFormViewModel
        {
            ApiError = GetTempApiError() ?? definitionsResult.Error ?? currenciesResult.Error,
            SubmitAction = nameof(Create),
            SubmitButtonText = "Отправить заявку на продажу",
            CriterionDefinitions = definitionsResult.Data ?? [],
            Currencies = currenciesResult.Data ?? []
        });
    }

    [HttpPost]
    [Authorize(Roles = "Client")]
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
        string? comment,
        string? criteriaJson,
        List<IFormFile>? photos,
        CancellationToken cancellationToken = default)
    {
        if (!ValidateRequiredFields(title, address, type))
        {
            TempData[ErrorKey] = "Заполните обязательные поля заявки.";
            return RedirectToAction(nameof(Create));
        }

        if (!TryParseCoordinates(latitude, longitude, out var parsedLatitude, out var parsedLongitude))
        {
            TempData[ErrorKey] = "Проверьте координаты объекта.";
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

        var payload = BuildPayload(
            title,
            address,
            price,
            priceCurrency,
            area,
            roomsCount,
            parsedLatitude,
            parsedLongitude,
            type,
            savedPhotos,
            comment,
            ParseCriteria(criteriaJson));

        var result = await _dealRequestsApiClient.CreateMySaleRequestAsync(payload, cancellationToken);
        if (result.Error?.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return RedirectToLoginCurrent();
        }

        if (result.IsSuccess)
        {
            TempData[SuccessKey] = "Заявка на продажу отправлена.";
            return RedirectToAction(nameof(Index));
        }

        TempData[ErrorKey] = result.Error?.Message ?? "Не удалось отправить заявку на продажу.";
        return RedirectToAction(nameof(Create));
    }

    [HttpGet]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken = default)
    {
        var detailsTask = _dealRequestsApiClient.GetMySaleRequestByIdAsync(id, cancellationToken);
        var definitionsTask = _realEstateApiClient.GetCriterionDefinitionsAsync(
            500,
            includeHidden: false,
            cancellationToken);
        var currenciesTask = _realEstateApiClient.GetCurrenciesAsync(200, includeInactive: false, cancellationToken);

        await Task.WhenAll(detailsTask, definitionsTask, currenciesTask);

        if (IsUnauthorized(detailsTask.Result.Error)
            || IsUnauthorized(definitionsTask.Result.Error)
            || IsUnauthorized(currenciesTask.Result.Error))
        {
            return RedirectToLoginCurrent();
        }

        var details = detailsTask.Result.Data;
        if (details is null)
        {
            return View("Form", new SellPropertyFormViewModel
            {
                ApiError = detailsTask.Result.Error ?? GetTempApiError(),
                DealId = id,
                SubmitAction = nameof(Update),
                SubmitButtonText = "Сохранить изменения",
                CanEdit = false
            });
        }

        var canEdit = string.Equals(details.Deal.Status, "Created", StringComparison.OrdinalIgnoreCase);

        return View("Form", new SellPropertyFormViewModel
        {
            SuccessMessage = TempData[SuccessKey]?.ToString(),
            ApiError = GetTempApiError() ?? detailsTask.Result.Error ?? definitionsTask.Result.Error,
            DealId = id,
            DealStatus = details.Deal.Status,
            CanEdit = canEdit,
            SubmitAction = nameof(Update),
            SubmitButtonText = "Сохранить изменения",
            Comment = details.Deal.RequestMessage,
            Form = new RealEstatePropertyFormValues
            {
                Title = details.Property.Title,
                Address = details.Property.Address,
                Price = details.Property.OriginalPriceAmount,
                PriceCurrency = details.Property.OriginalPriceCurrency,
                Area = details.Property.Area,
                RoomsCount = details.Property.RoomsCount,
                Latitude = details.Property.Latitude,
                Longitude = details.Property.Longitude,
                Type = details.Property.Type,
                OwnerFullName = details.Property.OwnerFullName ?? string.Empty,
                OwnerEmail = details.Property.OwnerEmail ?? string.Empty,
                OwnerPhoneNumber = details.Property.OwnerPhoneNumber ?? string.Empty
            },
            ExistingPhotoPaths = details.Property.PhotoPaths,
            CriterionDefinitions = definitionsTask.Result.Data ?? [],
            InitialCriteria = MapInitialCriteria(details.Property.Criteria),
            Currencies = currenciesTask.Result.Data ?? [],
            Lifecycle = details.Lifecycle
        });
    }

    [HttpGet]
    [Authorize(Policy = "RequireRealtorOrAdmin")]
    public async Task<IActionResult> EditManaged(
        Guid id,
        string scope = "incoming",
        string? source = null,
        string? search = null,
        string? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var detailsTask = _dealRequestsApiClient.GetSaleRequestByIdAsync(id, cancellationToken);
        var definitionsTask = _realEstateApiClient.GetCriterionDefinitionsAsync(
            500,
            includeHidden: false,
            cancellationToken);
        var currenciesTask = _realEstateApiClient.GetCurrenciesAsync(200, includeInactive: false, cancellationToken);

        await Task.WhenAll(detailsTask, definitionsTask, currenciesTask);

        if (IsUnauthorized(detailsTask.Result.Error)
            || IsUnauthorized(definitionsTask.Result.Error)
            || IsUnauthorized(currenciesTask.Result.Error))
        {
            return RedirectToLoginCurrent();
        }

        var details = detailsTask.Result.Data;
        if (details is null)
        {
            return View("Form", new SellPropertyFormViewModel
            {
                ApiError = detailsTask.Result.Error ?? GetTempApiError(),
                DealId = id,
                CanEdit = false,
                IsManagedMode = true,
                SubmitAction = nameof(UpdateManaged),
                SubmitButtonText = "Сохранить изменения",
                BackController = "DealRequests",
                BackAction = "Details",
                ReturnScope = scope,
                ReturnSource = source,
                ReturnSearch = search,
                ReturnStatus = status,
                ReturnPage = Math.Max(page, 1),
                ReturnPageSize = Math.Clamp(pageSize, 10, 100)
            });
        }

        var canEdit = string.Equals(details.Deal.Status, "Created", StringComparison.OrdinalIgnoreCase)
                      || string.Equals(details.Deal.Status, "InProgress", StringComparison.OrdinalIgnoreCase);

        return View("Form", new SellPropertyFormViewModel
        {
            SuccessMessage = TempData[SuccessKey]?.ToString(),
            ApiError = GetTempApiError() ?? detailsTask.Result.Error ?? definitionsTask.Result.Error,
            DealId = id,
            DealStatus = details.Deal.Status,
            CanEdit = canEdit,
            IsManagedMode = true,
            SubmitAction = nameof(UpdateManaged),
            SubmitButtonText = "Сохранить изменения",
            BackController = "DealRequests",
            BackAction = "Details",
            ReturnScope = scope,
            ReturnSource = source,
            ReturnSearch = search,
            ReturnStatus = status,
            ReturnPage = Math.Max(page, 1),
            ReturnPageSize = Math.Clamp(pageSize, 10, 100),
            Comment = details.Deal.RequestMessage,
            Lifecycle = details.Lifecycle,
            Form = new RealEstatePropertyFormValues
            {
                Title = details.Property.Title,
                Address = details.Property.Address,
                Price = details.Property.OriginalPriceAmount,
                PriceCurrency = details.Property.OriginalPriceCurrency,
                Area = details.Property.Area,
                RoomsCount = details.Property.RoomsCount,
                Latitude = details.Property.Latitude,
                Longitude = details.Property.Longitude,
                Type = details.Property.Type,
                OwnerFullName = details.Property.OwnerFullName ?? string.Empty,
                OwnerEmail = details.Property.OwnerEmail ?? string.Empty,
                OwnerPhoneNumber = details.Property.OwnerPhoneNumber ?? string.Empty
            },
            ExistingPhotoPaths = details.Property.PhotoPaths,
            CriterionDefinitions = definitionsTask.Result.Data ?? [],
            InitialCriteria = MapInitialCriteria(details.Property.Criteria),
            Currencies = currenciesTask.Result.Data ?? []
        });
    }

    [HttpPost]
    [Authorize(Roles = "Client")]
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
        string? comment,
        string? criteriaJson,
        List<string>? retainPhotoPaths,
        List<IFormFile>? newPhotos,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            TempData[ErrorKey] = "Не найдена заявка для обновления.";
            return RedirectToAction(nameof(Index));
        }

        if (!ValidateRequiredFields(title, address, type))
        {
            TempData[ErrorKey] = "Заполните обязательные поля заявки.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        if (!TryParseCoordinates(latitude, longitude, out var parsedLatitude, out var parsedLongitude))
        {
            TempData[ErrorKey] = "Проверьте координаты объекта.";
            return RedirectToAction(nameof(Edit), new { id });
        }

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

        if (finalPhotoPaths.Count > 20)
        {
            TempData[ErrorKey] = "Можно сохранить не более 20 фотографий.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var payload = BuildPayload(
            title,
            address,
            price,
            priceCurrency,
            area,
            roomsCount,
            parsedLatitude,
            parsedLongitude,
            type,
            finalPhotoPaths,
            comment,
            ParseCriteria(criteriaJson));

        var result = await _dealRequestsApiClient.UpdateMySaleRequestAsync(id, payload, cancellationToken);
        if (result.Error?.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return RedirectToLoginCurrent();
        }

        if (result.IsSuccess)
        {
            TempData[SuccessKey] = "Заявка обновлена.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        TempData[ErrorKey] = result.Error?.Message ?? "Не удалось обновить заявку.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [Authorize(Policy = "RequireRealtorOrAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateManaged(
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
        string? comment,
        string? criteriaJson,
        List<string>? retainPhotoPaths,
        List<IFormFile>? newPhotos,
        string scope = "incoming",
        string? source = null,
        string? search = null,
        string? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            TempData[ErrorKey] = "Не найдена заявка для обновления.";
            return RedirectToAction("Index", "DealRequests");
        }

        if (!ValidateRequiredFields(title, address, type))
        {
            TempData[ErrorKey] = "Заполните обязательные поля заявки.";
            return RedirectToAction(nameof(EditManaged), new { id, scope, source, search, status, page, pageSize });
        }

        if (!TryParseCoordinates(latitude, longitude, out var parsedLatitude, out var parsedLongitude))
        {
            TempData[ErrorKey] = "Проверьте координаты объекта.";
            return RedirectToAction(nameof(EditManaged), new { id, scope, source, search, status, page, pageSize });
        }

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

        if (finalPhotoPaths.Count > 20)
        {
            TempData[ErrorKey] = "Можно сохранить не более 20 фотографий.";
            return RedirectToAction(nameof(EditManaged), new { id, scope, source, search, status, page, pageSize });
        }

        var payload = BuildPayload(
            title,
            address,
            price,
            priceCurrency,
            area,
            roomsCount,
            parsedLatitude,
            parsedLongitude,
            type,
            finalPhotoPaths,
            comment,
            ParseCriteria(criteriaJson));

        var result = await _dealRequestsApiClient.UpdateSaleRequestAsync(id, payload, cancellationToken);
        if (result.Error?.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return RedirectToLoginCurrent();
        }

        if (result.IsSuccess)
        {
            TempData[SuccessKey] = "Заявка обновлена.";
            return RedirectToAction("Details", "DealRequests", new
            {
                id,
                scope,
                source,
                search,
                status,
                page = Math.Max(page, 1),
                pageSize = Math.Clamp(pageSize, 10, 100)
            });
        }

        TempData[ErrorKey] = result.Error?.Message ?? "Не удалось обновить заявку.";
        return RedirectToAction(nameof(EditManaged), new { id, scope, source, search, status, page, pageSize });
    }

    [HttpPost]
    [Authorize(Roles = "Client")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            TempData[ErrorKey] = "Не найдена заявка для отмены.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _dealRequestsApiClient.CancelMySaleRequestAsync(id, cancellationToken);
        if (result.Error?.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return RedirectToLoginCurrent();
        }

        if (result.IsSuccess)
        {
            TempData[SuccessKey] = "Заявка отменена.";
        }
        else
        {
            TempData[ErrorKey] = result.Error?.Message ?? "Не удалось отменить заявку.";
        }

        return RedirectToAction(nameof(Index));
    }

    private static bool ValidateRequiredFields(
        string title,
        string address,
        string type)
    {
        return !string.IsNullOrWhiteSpace(title)
               && !string.IsNullOrWhiteSpace(address)
               && !string.IsNullOrWhiteSpace(type);
    }

    private static DealRequestsApiClient.SaleRequestSubmitPayload BuildPayload(
        string title,
        string address,
        decimal price,
        string priceCurrency,
        double area,
        int roomsCount,
        double latitude,
        double longitude,
        string type,
        IReadOnlyList<string> photoPaths,
        string? comment,
        IReadOnlyList<RealEstateCriterionSubmitModel> criteria)
    {
        return new DealRequestsApiClient.SaleRequestSubmitPayload
        {
            Title = title.Trim(),
            Address = address.Trim(),
            Price = price,
            PriceCurrency = priceCurrency.Trim().ToUpperInvariant(),
            Area = area,
            RoomsCount = roomsCount,
            Latitude = latitude,
            Longitude = longitude,
            Type = type.Trim(),
            PhotoPaths = photoPaths,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            Criteria = criteria
                .Select(x => new DealRequestsApiClient.SaleRequestSubmitCriterionPayload
                {
                    CriterionDefinitionId = x.CriterionDefinitionId,
                    Value = string.IsNullOrWhiteSpace(x.Value) ? null : x.Value.Trim(),
                    Values = x.Values?
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Select(v => v.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList()
                })
                .ToList()
        };
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

    private static IReadOnlyList<RealEstateCriterionSubmitModel> MapInitialCriteria(
        IReadOnlyList<SaleRequestPropertyCriterionViewModel> criteria)
    {
        if (criteria.Count == 0)
            return [];

        return criteria
            .Select(item =>
            {
                if (string.Equals(item.ValueType, "MultiSelect", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var parsedValues = JsonSerializer.Deserialize<List<string>>(item.RawValue, CriteriaJsonSerializerOptions)
                            ?? [];

                        return new RealEstateCriterionSubmitModel
                        {
                            CriterionDefinitionId = item.CriterionDefinitionId,
                            Values = parsedValues
                        };
                    }
                    catch (JsonException)
                    {
                        return new RealEstateCriterionSubmitModel
                        {
                            CriterionDefinitionId = item.CriterionDefinitionId,
                            Values = item.RawValue
                                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                .ToList()
                        };
                    }
                }

                return new RealEstateCriterionSubmitModel
                {
                    CriterionDefinitionId = item.CriterionDefinitionId,
                    Value = item.RawValue
                };
            })
            .ToList();
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
}
