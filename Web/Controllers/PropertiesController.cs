using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.Models.Api;
using Web.Models.Dashboard;
using Web.Models.Properties;
using Web.Models.RealtorEfficiency;
using Web.Services;

namespace Web.Controllers;

[Authorize(Policy = "RequireAnyAuthorized")]
public class PropertiesController : Controller
{
    private const string SuccessKey = "DashboardSuccess";
    private const string ErrorKey = "DashboardErrorMessage";
    private const string PropertyDetailsSuccessKey = "PropertyDetailsSuccess";
    private const string PropertyDetailsErrorKey = "PropertyDetailsError";

    private readonly PropertiesApiClient _propertiesApiClient;
    private readonly RealtorDashboardApiClient _realtorDashboardApiClient;
    private readonly DealRequestsApiClient _dealRequestsApiClient;

    public PropertiesController(
        PropertiesApiClient propertiesApiClient,
        RealtorDashboardApiClient realtorDashboardApiClient,
        DealRequestsApiClient dealRequestsApiClient)
    {
        _propertiesApiClient = propertiesApiClient;
        _realtorDashboardApiClient = realtorDashboardApiClient;
        _dealRequestsApiClient = dealRequestsApiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        Guid? dealsByRealtorId,
        Guid? dealsByClientId,
        Guid? activityByRealtorId,
        Guid? subscribersPropertyId,
        CancellationToken cancellationToken)
    {
        if (!IsRealtorOrAdmin())
        {
            var homeUrl = Url.Action("Index", "Home") ?? "/";
            return Redirect($"{homeUrl}#catalog");
        }

        ApiErrorViewModel? apiError = GetTempApiError();
        var successMessage = TempData[SuccessKey]?.ToString();

        var properties = Array.Empty<PropertySummaryViewModel>();
        var deals = Array.Empty<DashboardDealViewModel>();
        var activityLogs = Array.Empty<DashboardActivityLogViewModel>();
        DashboardPropertySubscribersViewModel? subscribersInfo = null;

        var propertiesResult = await _realtorDashboardApiClient.GetPropertiesAsync(200, cancellationToken);
        if (propertiesResult.Error?.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return RedirectToLoginCurrent();
        }

        if (propertiesResult.Data is not null)
        {
            properties = [.. propertiesResult.Data];
        }
        else
        {
            apiError ??= propertiesResult.Error;
        }

        ApiClientResult<IReadOnlyList<DashboardDealViewModel>> dealsResult;
        if (dealsByRealtorId.HasValue && dealsByRealtorId.Value != Guid.Empty)
        {
            dealsResult = await _realtorDashboardApiClient.GetDealsByRealtorAsync(dealsByRealtorId.Value, cancellationToken);
        }
        else if (dealsByClientId.HasValue && dealsByClientId.Value != Guid.Empty)
        {
            dealsResult = await _realtorDashboardApiClient.GetDealsByClientAsync(dealsByClientId.Value, cancellationToken);
        }
        else
        {
            dealsResult = await _realtorDashboardApiClient.GetDealsAsync(200, cancellationToken);
        }

        if (dealsResult.Error?.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return RedirectToLoginCurrent();
        }

        if (dealsResult.Data is not null)
        {
            deals = [.. dealsResult.Data];
        }
        else
        {
            apiError ??= dealsResult.Error;
        }

        ApiClientResult<IReadOnlyList<DashboardActivityLogViewModel>> activityResult;
        if (activityByRealtorId.HasValue && activityByRealtorId.Value != Guid.Empty)
        {
            activityResult = await _realtorDashboardApiClient.GetActivityLogsByRealtorAsync(activityByRealtorId.Value, cancellationToken);
        }
        else
        {
            activityResult = await _realtorDashboardApiClient.GetActivityLogsAsync(200, cancellationToken);
        }

        if (activityResult.Error?.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return RedirectToLoginCurrent();
        }

        if (activityResult.Data is not null)
        {
            activityLogs = [.. activityResult.Data];
        }
        else
        {
            apiError ??= activityResult.Error;
        }

        if (subscribersPropertyId.HasValue && subscribersPropertyId.Value != Guid.Empty)
        {
            var subscribersResult = await _realtorDashboardApiClient.GetSubscribedClientsCountAsync(
                subscribersPropertyId.Value,
                cancellationToken: cancellationToken);

            if (subscribersResult.Error?.StatusCode == StatusCodes.Status401Unauthorized)
            {
                return RedirectToLoginCurrent();
            }

            if (subscribersResult.Error is null)
            {
                subscribersInfo = new DashboardPropertySubscribersViewModel
                {
                    PropertyId = subscribersPropertyId.Value,
                    ClientsCount = subscribersResult.Data
                };
            }
            else
            {
                apiError ??= subscribersResult.Error;
            }
        }

        return View(new RealtorDashboardViewModel
        {
            SuccessMessage = successMessage,
            ApiError = apiError,
            Properties = properties,
            Deals = deals,
            ActivityLogs = activityLogs,
            DealsByRealtorId = dealsByRealtorId,
            DealsByClientId = dealsByClientId,
            ActivityByRealtorId = activityByRealtorId,
            SubscribersInfo = subscribersInfo,
            ImprovementHints = BuildImprovementHints(properties, deals, activityLogs)
        });
    }

    [HttpPost]
    [Authorize(Policy = "RequireRealtorOrAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateProperty(
        string title,
        string address,
        decimal price,
        double area,
        int roomsCount,
        double latitude,
        double longitude,
        string type,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(title)
            || string.IsNullOrWhiteSpace(address)
            || string.IsNullOrWhiteSpace(type)
            || price <= 0
            || area <= 0
            || roomsCount <= 0)
        {
            TempData[ErrorKey] = "Please provide valid property data.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _realtorDashboardApiClient.CreatePropertyAsync(
            title.Trim(),
            address.Trim(),
            price,
            area,
            roomsCount,
            latitude,
            longitude,
            type.Trim(),
            cancellationToken);

        SetOperationResult(result, "Property has been created.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Policy = "RequireRealtorOrAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePropertyPrice(
        Guid propertyId,
        decimal newPrice,
        CancellationToken cancellationToken)
    {
        if (propertyId == Guid.Empty || newPrice <= 0)
        {
            TempData[ErrorKey] = "Please provide a valid property id and price.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _realtorDashboardApiClient.UpdatePropertyPriceAsync(propertyId, newPrice, cancellationToken);
        SetOperationResult(result, "Property price has been updated.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Policy = "RequireRealtorOrAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsSold(Guid propertyId, CancellationToken cancellationToken)
    {
        if (propertyId == Guid.Empty)
        {
            TempData[ErrorKey] = "Property id is required.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _realtorDashboardApiClient.MarkPropertyAsSoldAsync(propertyId, cancellationToken);
        SetOperationResult(result, "Property status has been changed to sold.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Policy = "RequireRealtorOrAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteDeal(
        Guid dealId,
        decimal commissionAmount,
        string commissionCurrency = "USD",
        CancellationToken cancellationToken = default)
    {
        if (dealId == Guid.Empty || commissionAmount < 0)
        {
            TempData[ErrorKey] = "Please provide a valid deal id and commission amount.";
            return RedirectToAction(nameof(Index));
        }

        var safeCurrency = string.IsNullOrWhiteSpace(commissionCurrency)
            ? "USD"
            : commissionCurrency.Trim().ToUpperInvariant();

        var result = await _realtorDashboardApiClient.CompleteDealAsync(
            dealId,
            commissionAmount,
            safeCurrency,
            cancellationToken);

        SetOperationResult(result, "Deal has been completed.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Policy = "RequireRealtorOrAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelDeal(Guid dealId, CancellationToken cancellationToken)
    {
        if (dealId == Guid.Empty)
        {
            TempData[ErrorKey] = "Deal id is required.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _realtorDashboardApiClient.CancelDealAsync(dealId, cancellationToken);
        SetOperationResult(result, "Deal has been cancelled.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Policy = "RequireAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateActivityLog(
        Guid realtorId,
        string type,
        int points,
        CancellationToken cancellationToken)
    {
        if (realtorId == Guid.Empty || string.IsNullOrWhiteSpace(type))
        {
            TempData[ErrorKey] = "Realtor id and activity type are required.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _realtorDashboardApiClient.CreateActivityLogAsync(
            realtorId,
            type.Trim(),
            points,
            cancellationToken);

        SetOperationResult(result, "Activity log has been created.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Policy = "RequireAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateActivityLog(
        Guid activityLogId,
        string type,
        int points,
        CancellationToken cancellationToken)
    {
        if (activityLogId == Guid.Empty || string.IsNullOrWhiteSpace(type))
        {
            TempData[ErrorKey] = "Activity log id and type are required.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _realtorDashboardApiClient.UpdateActivityLogAsync(
            activityLogId,
            type.Trim(),
            points,
            cancellationToken);

        SetOperationResult(result, "Activity log has been updated.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Policy = "RequireAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteActivityLog(Guid activityLogId, CancellationToken cancellationToken)
    {
        if (activityLogId == Guid.Empty)
        {
            TempData[ErrorKey] = "Activity log id is required.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _realtorDashboardApiClient.DeleteActivityLogAsync(activityLogId, cancellationToken);
        SetOperationResult(result, "Activity log has been deleted.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Details(
        Guid id,
        string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        var backNavigation = ResolveDetailsBackNavigation(returnUrl);
        var propertyResult = await _propertiesApiClient.GetByIdAsync(id, cancellationToken);
        if (propertyResult.Error is not null || propertyResult.Data is null)
        {
            return View(new PropertyDetailsViewModel
            {
                ApiError = propertyResult.Error ?? GetPropertyDetailsTempApiError(),
                SuccessMessage = TempData[PropertyDetailsSuccessKey]?.ToString(),
                BackUrl = backNavigation.BackUrl,
                UseHistoryBack = backNavigation.UseHistoryBack
            });
        }

        var criteriaResult = await _propertiesApiClient.GetCriteriaAsync(id, cancellationToken);
        var apiError = criteriaResult.Error ?? GetPropertyDetailsTempApiError();

        return View(new PropertyDetailsViewModel
        {
            SuccessMessage = TempData[PropertyDetailsSuccessKey]?.ToString(),
            Property = propertyResult.Data,
            Criteria = criteriaResult.Data ?? [],
            ApiError = apiError,
            BackUrl = backNavigation.BackUrl,
            UseHistoryBack = backNavigation.UseHistoryBack
        });
    }

    [HttpPost]
    [Authorize(Roles = "Client")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitDealRequest(Guid propertyId, CancellationToken cancellationToken = default)
    {
        if (propertyId == Guid.Empty)
        {
            TempData[PropertyDetailsErrorKey] = "Не удалось определить объект для отправки заявки.";
            return RedirectToAction("Index", "Home");
        }

        var result = await _dealRequestsApiClient.CreateMyRequestAsync(
            propertyId,
            clientRequirementId: null,
            source: "Home",
            message: null,
            cancellationToken: cancellationToken);

        if (result.Error?.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return RedirectToLoginCurrent();
        }

        if (result.IsSuccess)
        {
            TempData[PropertyDetailsSuccessKey] = "Заявка отправлена. Риелтор свяжется с вами.";
        }
        else
        {
            TempData[PropertyDetailsErrorKey] = result.Error?.Message ?? "Не удалось отправить заявку.";
        }

        return RedirectToAction(nameof(Details), new { id = propertyId });
    }

    private bool IsRealtorOrAdmin()
    {
        return User.IsInRole("Realtor")
            || User.IsInRole("Admin")
            || User.IsInRole("SuperAdmin");
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

    private ApiErrorViewModel? GetPropertyDetailsTempApiError()
    {
        var message = TempData[PropertyDetailsErrorKey]?.ToString();
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

        TempData[ErrorKey] = result.Error?.Message ?? "Unable to complete the operation.";
    }

    private IActionResult RedirectToLoginCurrent()
    {
        var returnUrl = $"{Request.Path}{Request.QueryString}";
        return RedirectToAction("Login", "Auth", new { returnUrl });
    }

    private (string BackUrl, bool UseHistoryBack) ResolveDetailsBackNavigation(string? returnUrl)
    {
        if (string.Equals(returnUrl, "__history_back__", StringComparison.Ordinal))
        {
            return (Url.Action("Index", "Matching") ?? "/", true);
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return (returnUrl.Trim(), false);
        }

        return ($"{Url.Action("Index", "Home") ?? "/"}#catalog", false);
    }

    private static IReadOnlyList<RealtorImprovementHintViewModel> BuildImprovementHints(
        IReadOnlyList<PropertySummaryViewModel> properties,
        IReadOnlyList<DashboardDealViewModel> deals,
        IReadOnlyList<DashboardActivityLogViewModel> activityLogs)
    {
        var hints = new List<RealtorImprovementHintViewModel>();

        if (properties.Count == 0)
        {
            hints.Add(new RealtorImprovementHintViewModel
            {
                Severity = "Warning",
                Title = "Нужны активные объекты",
                Message = "Добавьте объекты и заполните их карточки, чтобы повысить качество аналитики."
            });
        }
        else
        {
            var withoutPhotos = properties.Count(x => string.IsNullOrWhiteSpace(x.MainPhotoPath));
            if (withoutPhotos > 0)
            {
                hints.Add(new RealtorImprovementHintViewModel
                {
                    Severity = "Warning",
                    Title = "Фото в карточках",
                    Message = $"У {withoutPhotos} объектов нет главного фото. Карточки с фото получают лучшие оценки."
                });
            }
            else
            {
                hints.Add(new RealtorImprovementHintViewModel
                {
                    Severity = "Success",
                    Title = "Фото в порядке",
                    Message = "У всех текущих объектов есть главное фото."
                });
            }
        }

        var completedDeals = deals.Count(x => string.Equals(x.Status, "Completed", StringComparison.OrdinalIgnoreCase));
        var cancelledDeals = deals.Count(x => string.Equals(x.Status, "Cancelled", StringComparison.OrdinalIgnoreCase));
        var completedOrCancelled = completedDeals + cancelledDeals;

        if (completedOrCancelled > 0)
        {
            var cancellationRate = cancelledDeals / (double)completedOrCancelled * 100d;
            hints.Add(new RealtorImprovementHintViewModel
            {
                Severity = cancellationRate > 25 ? "Warning" : "Success",
                Title = "Дисциплина по сделкам",
                Message = $"Отмены: {cancelledDeals} из {completedOrCancelled} ({cancellationRate:F1}%)."
            });
        }

        var fromUtc = DateTime.UtcNow.AddDays(-30);
        var last30Points = activityLogs
            .Where(x => x.CreatedDate >= fromUtc)
            .Sum(x => x.Points);

        hints.Add(new RealtorImprovementHintViewModel
        {
            Severity = last30Points < 30 ? "Warning" : "Success",
            Title = "Активность за 30 дней",
            Message = $"Начислено {last30Points} activity points. Для стабильного APS держите высокий темп действий."
        });

        if (hints.Count == 0)
        {
            hints.Add(new RealtorImprovementHintViewModel
            {
                Severity = "Info",
                Title = "Пока без замечаний",
                Message = "Продолжайте в том же темпе и следите за качеством карточек объектов."
            });
        }

        return hints;
    }
}
