using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.Models.Api;
using Web.Models.DealCenter;
using Web.Models.DealRequests;
using Web.Services;

namespace Web.Controllers;

[Authorize(Roles = "Client")]
public class DealCenterController : Controller
{
    private const string SuccessKey = "DealCenterSuccess";
    private const string ErrorKey = "DealCenterError";

    private readonly DealRequestsApiClient _dealRequestsApiClient;
    private readonly PropertiesApiClient _propertiesApiClient;
    private readonly RealtorEfficiencyApiClient _realtorEfficiencyApiClient;
    private readonly ComplaintsApiClient _complaintsApiClient;
    private readonly DealChatApiClient _dealChatApiClient;

    public DealCenterController(
        DealRequestsApiClient dealRequestsApiClient,
        PropertiesApiClient propertiesApiClient,
        RealtorEfficiencyApiClient realtorEfficiencyApiClient,
        ComplaintsApiClient complaintsApiClient,
        DealChatApiClient dealChatApiClient)
    {
        _dealRequestsApiClient = dealRequestsApiClient;
        _propertiesApiClient = propertiesApiClient;
        _realtorEfficiencyApiClient = realtorEfficiencyApiClient;
        _complaintsApiClient = complaintsApiClient;
        _dealChatApiClient = dealChatApiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string scope = "purchase",
        string? search = null,
        string? status = null,
        int limit = 200,
        CancellationToken cancellationToken = default)
    {
        var normalizedScope = NormalizeScope(scope);
        var dealsResult = await _dealRequestsApiClient.GetMyClientDealsAsync(
            Math.Clamp(limit, 1, 500),
            search,
            status,
            normalizedScope,
            cancellationToken);

        if (dealsResult.Error?.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return RedirectToLoginCurrent();
        }

        var unreadMap = new Dictionary<Guid, int>();
        var unreadResult = await _dealChatApiClient.GetUnreadNotificationsAsync(200, cancellationToken);
        if (unreadResult.Data is not null)
        {
            unreadMap = unreadResult.Data
                .Where(x => x.DealId != Guid.Empty && x.UnreadCount > 0)
                .GroupBy(x => x.DealId)
                .ToDictionary(x => x.Key, x => x.Sum(y => Math.Max(0, y.UnreadCount)));
        }

        return View(new DealCenterIndexViewModel
        {
            SuccessMessage = TempData[SuccessKey]?.ToString(),
            ApiError = GetTempApiError() ?? dealsResult.Error,
            Scope = normalizedScope,
            Search = search,
            Status = status,
            Items = dealsResult.Data ?? [],
            UnreadMessagesByDealId = unreadMap
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        Guid id,
        string scope = "purchase",
        string? search = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedScope = NormalizeScope(scope);
        var dealResult = await _dealRequestsApiClient.GetMyClientDealByIdAsync(id, cancellationToken);

        if (dealResult.Error?.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return RedirectToLoginCurrent();
        }

        var deal = dealResult.Data;
        var apiError = GetTempApiError() ?? dealResult.Error;

        Web.Models.Properties.PropertySummaryViewModel? property = null;
        Web.Models.RealtorEfficiency.DealFeedbackStateViewModel? feedbackState = null;

        if (deal?.PropertyId.HasValue == true)
        {
            var propertyTask = _propertiesApiClient.GetByIdAsync(deal.PropertyId.Value, cancellationToken);
            var stateTask = _realtorEfficiencyApiClient.GetFeedbackStateAsync(id, cancellationToken);

            await Task.WhenAll(propertyTask, stateTask);

            if (propertyTask.Result.Error?.StatusCode == StatusCodes.Status401Unauthorized
                || stateTask.Result.Error?.StatusCode == StatusCodes.Status401Unauthorized)
            {
                return RedirectToLoginCurrent();
            }

            property = propertyTask.Result.Data;
            feedbackState = stateTask.Result.Data;
            apiError ??= propertyTask.Result.Error ?? stateTask.Result.Error;
        }
        else if (deal is not null)
        {
            var stateResult = await _realtorEfficiencyApiClient.GetFeedbackStateAsync(id, cancellationToken);
            if (stateResult.Error?.StatusCode == StatusCodes.Status401Unauthorized)
            {
                return RedirectToLoginCurrent();
            }

            feedbackState = stateResult.Data;
            apiError ??= stateResult.Error;
        }

        return View(new DealCenterDetailsViewModel
        {
            SuccessMessage = TempData[SuccessKey]?.ToString(),
            ApiError = apiError,
            Deal = deal,
            Property = property,
            FeedbackState = feedbackState,
            Scope = normalizedScope,
            Search = search,
            Status = status
        });
    }

    [HttpGet]
    public async Task<IActionResult> Complaint(
        Guid? dealId = null,
        Guid? propertyId = null,
        string source = "deal",
        string scope = "purchase",
        string? search = null,
        string? status = null,
        string? category = null,
        string? subject = null,
        string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedSource = NormalizeComplaintSource(source);
        var normalizedScope = NormalizeScope(scope);

        DealRequestListItemViewModel? deal = null;
        Web.Models.Properties.PropertySummaryViewModel? property = null;
        ApiErrorViewModel? apiError = GetTempApiError();

        if (dealId.HasValue)
        {
            var dealResult = await _dealRequestsApiClient.GetMyClientDealByIdAsync(dealId.Value, cancellationToken);
            if (dealResult.Error?.StatusCode == StatusCodes.Status401Unauthorized)
            {
                return RedirectToLoginCurrent();
            }

            deal = dealResult.Data;
            apiError ??= dealResult.Error;

            if (deal is null)
            {
                TempData[ErrorKey] = dealResult.Error?.Message ?? "Сделка не найдена.";
                return RedirectToAction(nameof(Index), new { scope = normalizedScope, search, status });
            }

            if (deal.PropertyId.HasValue)
            {
                var propertyResult = await _propertiesApiClient.GetByIdAsync(deal.PropertyId.Value, cancellationToken);
                if (propertyResult.Error?.StatusCode == StatusCodes.Status401Unauthorized)
                {
                    return RedirectToLoginCurrent();
                }

                property = propertyResult.Data;
                apiError ??= propertyResult.Error;
            }
        }
        else if (propertyId.HasValue)
        {
            var propertyResult = await _propertiesApiClient.GetByIdAsync(propertyId.Value, cancellationToken);
            if (propertyResult.Error?.StatusCode == StatusCodes.Status401Unauthorized)
            {
                return RedirectToLoginCurrent();
            }

            property = propertyResult.Data;
            apiError ??= propertyResult.Error;
        }

        var categories = BuildComplaintCategories(normalizedSource, deal);
        var defaultCategory = NormalizeComplaintCategory(category);
        if (!categories.Any(x => x.Value == defaultCategory))
        {
            defaultCategory = categories[0].Value;
        }

        var defaultSubject = BuildDefaultComplaintSubject(normalizedSource, deal, property, defaultCategory);
        var backUrl = ResolveComplaintBackUrl(normalizedSource, dealId, propertyId, normalizedScope, search, status, returnUrl);

        return View(new DealComplaintPageViewModel
        {
            SuccessMessage = TempData[SuccessKey]?.ToString(),
            ApiError = apiError,
            Deal = deal,
            Property = property,
            Source = normalizedSource,
            Scope = normalizedScope,
            Search = search,
            Status = status,
            DealId = dealId,
            PropertyId = propertyId,
            Category = defaultCategory,
            Subject = string.IsNullOrWhiteSpace(subject) ? defaultSubject : subject.Trim(),
            CategoryOptions = categories,
            BackUrl = backUrl
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitComplaintFromPage(
        Guid? dealId = null,
        Guid? propertyId = null,
        string source = "deal",
        string scope = "purchase",
        string? search = null,
        string? status = null,
        string? returnUrl = null,
        string category = "Other",
        string subject = "",
        string description = "",
        CancellationToken cancellationToken = default)
    {
        var normalizedSource = NormalizeComplaintSource(source);
        var normalizedScope = NormalizeScope(scope);

        if (string.IsNullOrWhiteSpace(description))
        {
            TempData[ErrorKey] = "Заполните описание жалобы.";
            return RedirectToAction(nameof(Complaint), new { dealId, propertyId, source = normalizedSource, scope = normalizedScope, search, status, category, subject, returnUrl });
        }

        DealRequestListItemViewModel? deal = null;
        Guid? targetRealtorId = null;
        Guid? linkedDealId = null;

        if (dealId.HasValue)
        {
            var dealResult = await _dealRequestsApiClient.GetMyClientDealByIdAsync(dealId.Value, cancellationToken);
            if (dealResult.Error?.StatusCode == StatusCodes.Status401Unauthorized)
            {
                return RedirectToLoginCurrent();
            }

            deal = dealResult.Data;
            if (deal is null)
            {
                TempData[ErrorKey] = dealResult.Error?.Message ?? "Сделка не найдена.";
                return RedirectToAction(nameof(Index), new { scope = normalizedScope, search, status });
            }

            linkedDealId = deal.Id;
            targetRealtorId = deal.RealtorId.HasValue && deal.RealtorId.Value != Guid.Empty
                ? deal.RealtorId
                : null;
            propertyId ??= deal.PropertyId;
        }

        var allowedCategories = BuildComplaintCategories(normalizedSource, deal);
        var normalizedCategory = NormalizeComplaintCategory(category);
        if (!allowedCategories.Any(x => x.Value == normalizedCategory))
        {
            TempData[ErrorKey] = "Выбрана недопустимая категория жалобы для этого раздела.";
            return RedirectToAction(nameof(Complaint), new { dealId, propertyId, source = normalizedSource, scope = normalizedScope, search, status, category = normalizedCategory, subject, returnUrl });
        }

        if (normalizedCategory == "Realtor" && !targetRealtorId.HasValue)
        {
            TempData[ErrorKey] = "Для жалобы на риелтора нужен назначенный риелтор в сделке.";
            return RedirectToAction(nameof(Complaint), new { dealId, propertyId, source = normalizedSource, scope = normalizedScope, search, status, category = normalizedCategory, subject, returnUrl });
        }

        var subjectToSend = BuildComplaintSubjectForSubmission(normalizedSource, normalizedCategory, deal);

        var result = await _complaintsApiClient.CreateComplaintAsync(
            normalizedCategory,
            subjectToSend,
            description.Trim(),
            linkedDealId,
            propertyId,
            targetRealtorId,
            cancellationToken);

        if (result.Error?.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return RedirectToLoginCurrent();
        }

        if (result.IsSuccess)
        {
            TempData[SuccessKey] = "Жалоба отправлена. Администратор рассмотрит ее.";
        }
        else
        {
            TempData[ErrorKey] = result.Error?.Message ?? "Не удалось отправить жалобу.";
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Complaint), new { dealId, propertyId, source = normalizedSource, scope = normalizedScope, search, status, category = normalizedCategory });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitFeedback(
        Guid id,
        string scope = "purchase",
        string? search = null,
        string? status = null,
        int serviceScore = 5,
        int? communicationScore = 5,
        int? responsivenessScore = 5,
        int? expertiseScore = 5,
        int? titleAccuracyScore = 5,
        int? descriptionAccuracyScore = 5,
        int? photosAccuracyScore = 5,
        int? criteriaAccuracyScore = 5,
        string? comment = null,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            TempData[ErrorKey] = "Не удалось определить сделку для feedback.";
            return RedirectToAction(nameof(Index), new { scope = NormalizeScope(scope), search, status });
        }

        if (serviceScore is < 1 or > 5)
        {
            TempData[ErrorKey] = "Оценка сервиса должна быть в диапазоне от 1 до 5.";
            return RedirectToAction(nameof(Details), new { id, scope = NormalizeScope(scope), search, status });
        }

        var stateResult = await _realtorEfficiencyApiClient.GetFeedbackStateAsync(id, cancellationToken);
        if (stateResult.Error?.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return RedirectToLoginCurrent();
        }

        var state = stateResult.Data;
        if (state is null || !state.CanSubmit)
        {
            TempData[ErrorKey] = state?.BlockReason ?? "Feedback сейчас недоступен.";
            return RedirectToAction(nameof(Details), new { id, scope = NormalizeScope(scope), search, status });
        }

        var isPurchase = state.IsPurchase;
        var result = await _realtorEfficiencyApiClient.SubmitFeedbackAsync(
            id,
            serviceScore,
            state.FormType,
            NormalizeOptionalScore(communicationScore),
            NormalizeOptionalScore(responsivenessScore),
            NormalizeOptionalScore(expertiseScore),
            isPurchase ? NormalizeOptionalScore(titleAccuracyScore) : null,
            isPurchase ? NormalizeOptionalScore(criteriaAccuracyScore) : null,
            isPurchase ? NormalizeOptionalScore(descriptionAccuracyScore) : null,
            isPurchase ? NormalizeOptionalScore(photosAccuracyScore) : null,
            string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            cancellationToken);

        if (result.Error?.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return RedirectToLoginCurrent();
        }

        if (result.IsSuccess)
        {
            TempData[SuccessKey] = "Спасибо! Feedback сохранен.";
        }
        else
        {
            TempData[ErrorKey] = result.Error?.Message ?? "Не удалось сохранить feedback.";
        }

        return RedirectToAction(nameof(Details), new { id, scope = NormalizeScope(scope), search, status });
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

    private IActionResult RedirectToLoginCurrent()
    {
        var returnUrl = $"{Request.Path}{Request.QueryString}";
        return RedirectToAction("Login", "Auth", new { returnUrl });
    }

    private static int? NormalizeOptionalScore(int? value)
    {
        if (!value.HasValue)
            return null;

        return value.Value is < 1 or > 5 ? null : value.Value;
    }

    private static string NormalizeScope(string? scope)
    {
        if (string.Equals(scope, "sale", StringComparison.OrdinalIgnoreCase))
            return "sale";

        if (string.Equals(scope, "purchase", StringComparison.OrdinalIgnoreCase))
            return "purchase";

        return "purchase";
    }

    private static string NormalizeComplaintCategory(string? rawValue)
    {
        return rawValue?.Trim() switch
        {
            "Realtor" => "Realtor",
            "PropertyDescriptionMismatch" => "PropertyDescriptionMismatch",
            "PoorPropertyMatching" => "PoorPropertyMatching",
            "Other" => "Other",
            _ => "Other"
        };
    }

    private static string NormalizeComplaintSource(string? rawValue)
    {
        return rawValue?.Trim().ToLowerInvariant() switch
        {
            "deal" => "deal",
            "property" => "property",
            "matching" => "matching",
            _ => "deal"
        };
    }

    private static List<ComplaintCategoryOptionViewModel> BuildComplaintCategories(
        string source,
        DealRequestListItemViewModel? deal)
    {
        if (source == "property")
        {
            return
            [
                new ComplaintCategoryOptionViewModel
                {
                    Value = "PropertyDescriptionMismatch",
                    Label = "Несоответствие описанию недвижимости"
                }
            ];
        }

        if (source == "matching")
        {
            return
            [
                new ComplaintCategoryOptionViewModel
                {
                    Value = "PoorPropertyMatching",
                    Label = "Плохие результаты подбора недвижимости"
                }
            ];
        }

        var isSale = string.Equals(deal?.Source, "Sale", StringComparison.OrdinalIgnoreCase);
        var hasRealtor = deal?.RealtorId.HasValue == true && deal.RealtorId.Value != Guid.Empty;

        if (isSale)
        {
            if (!hasRealtor)
            {
                return
                [
                    new ComplaintCategoryOptionViewModel { Value = "Other", Label = "Другое" }
                ];
            }

            return
            [
                new ComplaintCategoryOptionViewModel { Value = "Realtor", Label = "На риелтора" },
                new ComplaintCategoryOptionViewModel { Value = "Other", Label = "Другое" }
            ];
        }

        if (!hasRealtor)
        {
            return
            [
                new ComplaintCategoryOptionViewModel { Value = "PropertyDescriptionMismatch", Label = "Несоответствие описанию недвижимости" },
                new ComplaintCategoryOptionViewModel { Value = "Other", Label = "Другое" }
            ];
        }

        return
        [
            new ComplaintCategoryOptionViewModel { Value = "Realtor", Label = "На риелтора" },
            new ComplaintCategoryOptionViewModel { Value = "PropertyDescriptionMismatch", Label = "Несоответствие описанию недвижимости" },
            new ComplaintCategoryOptionViewModel { Value = "Other", Label = "Другое" }
        ];
    }

    private static string BuildDefaultComplaintSubject(
        string source,
        DealRequestListItemViewModel? deal,
        Web.Models.Properties.PropertySummaryViewModel? property,
        string category)
    {
        if (source == "matching")
            return "Жалоба на результаты подбора недвижимости";

        if (source == "property")
            return $"Жалоба по карточке объекта {(property?.Title ?? string.Empty)}".Trim();

        var baseSubject = category switch
        {
            "Realtor" => "Жалоба на работу риелтора",
            "PropertyDescriptionMismatch" => "Несоответствие описанию недвижимости",
            _ => "Жалоба по сделке"
        };

        if (deal is null)
            return baseSubject;

        return $"{baseSubject} (сделка {deal.Id})";
    }

    private string ResolveComplaintBackUrl(
        string source,
        Guid? dealId,
        Guid? propertyId,
        string scope,
        string? search,
        string? status,
        string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return returnUrl;

        if (source == "property" && propertyId.HasValue)
        {
            return Url.Action("Details", "Properties", new { id = propertyId.Value }) ?? "/";
        }

        if (source == "matching")
        {
            return Url.Action("Index", "Matching") ?? "/";
        }

        if (dealId.HasValue)
        {
            return Url.Action("Details", "DealCenter", new { id = dealId.Value, scope, search, status }) ?? "/DealCenter";
        }

        return Url.Action("Index", "DealCenter", new { scope, search, status }) ?? "/DealCenter";
    }

    private static string BuildComplaintSubjectForSubmission(
        string source,
        string category,
        DealRequestListItemViewModel? deal)
    {
        if (source == "matching")
            return "Жалоба на результаты подбора недвижимости";

        if (source == "property")
            return "Жалоба по карточке объекта";

        var baseSubject = category switch
        {
            "Realtor" => "Жалоба на работу риелтора",
            "PropertyDescriptionMismatch" => "Несоответствие описанию недвижимости",
            "PoorPropertyMatching" => "Плохие результаты подбора недвижимости",
            _ => "Жалоба по сделке"
        };

        if (deal is null)
            return baseSubject;

        return $"{baseSubject} (сделка {deal.Id})";
    }
}
