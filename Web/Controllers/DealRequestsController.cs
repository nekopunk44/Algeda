using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.Models.Api;
using Web.Models.DealRequests;
using Web.Services;

namespace Web.Controllers;

[Authorize(Policy = "RequireRealtorOrAdmin")]
public class DealRequestsController : Controller
{
    private const string SuccessKey = "DealRequestsSuccess";
    private const string ErrorKey = "DealRequestsError";

    private static readonly string[] OrderedStatuses =
    [
        "Created",
        "InProgress",
        "Completed",
        "Cancelled"
    ];

    private readonly DealRequestsApiClient _dealRequestsApiClient;
    private readonly PropertiesApiClient _propertiesApiClient;
    private readonly DealChatApiClient _dealChatApiClient;

    public DealRequestsController(
        DealRequestsApiClient dealRequestsApiClient,
        PropertiesApiClient propertiesApiClient,
        DealChatApiClient dealChatApiClient)
    {
        _dealRequestsApiClient = dealRequestsApiClient;
        _propertiesApiClient = propertiesApiClient;
        _dealChatApiClient = dealChatApiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string scope = "incoming",
        string? source = null,
        string? search = null,
        string? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = User.IsInRole("Admin");
        var isRealtor = User.IsInRole("Realtor");

        var normalizedScope = NormalizeScope(scope, isAdmin, isRealtor);
        var normalizedSource = NormalizeSource(source);
        var sourceFilter = MapSourceFilter(normalizedSource);
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 10, 100);

        Task<ApiClientResult<IReadOnlyList<DealRequestListItemViewModel>>> listTask =
            normalizedScope == "incoming"
                ? _dealRequestsApiClient.GetIncomingAsync(500, search, sourceFilter, cancellationToken)
                : isAdmin
                    ? _dealRequestsApiClient.GetAllAsync(500, search, status, sourceFilter, cancellationToken)
                    : _dealRequestsApiClient.GetMineAsync(500, search, status, sourceFilter, cancellationToken);

        var statusCountsTask = _dealRequestsApiClient.GetStatusCountsAsync(sourceFilter, cancellationToken);
        var unreadTask = _dealChatApiClient.GetUnreadNotificationsAsync(200, cancellationToken);
        await Task.WhenAll(listTask, statusCountsTask, unreadTask);

        if (IsUnauthorized(listTask.Result.Error)
            || IsUnauthorized(statusCountsTask.Result.Error)
            || IsUnauthorized(unreadTask.Result.Error))
        {
            return RedirectToLoginCurrent();
        }

        var apiError = GetTempApiError() ?? listTask.Result.Error ?? statusCountsTask.Result.Error;
        var items = listTask.Result.Data ?? [];

        if (string.Equals(normalizedSource, "purchase", StringComparison.OrdinalIgnoreCase))
        {
            items = items
                .Where(x => !string.Equals(x.Source, "Sale", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var statusCounts = string.Equals(normalizedSource, "purchase", StringComparison.OrdinalIgnoreCase)
            ? BuildStatusCountsFromItems(items)
            : BuildStatusCounts(statusCountsTask.Result.Data);

        if (normalizedScope == "incoming" && !string.IsNullOrWhiteSpace(status))
        {
            items = items
                .Where(x => string.Equals(x.Status, status, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var orderedItems = items
            .OrderByDescending(x => x.CreatedDate)
            .ToList();

        var totalCount = orderedItems.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Min(page, totalPages);

        var pageItems = orderedItems
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var unreadMap = new Dictionary<Guid, int>();
        if (unreadTask.Result.Data is not null)
        {
            unreadMap = unreadTask.Result.Data
                .Where(x => x.DealId != Guid.Empty && x.UnreadCount > 0)
                .GroupBy(x => x.DealId)
                .ToDictionary(x => x.Key, x => x.Sum(y => Math.Max(0, y.UnreadCount)));
        }

        return View(new DealRequestsIndexViewModel
        {
            SuccessMessage = TempData[SuccessKey]?.ToString(),
            ApiError = apiError,
            Filters = new DealRequestFilterViewModel
            {
                Scope = normalizedScope,
                Source = normalizedSource,
                Search = search,
                Status = status,
                Page = page,
                PageSize = pageSize
            },
            Items = pageItems,
            StatusCounts = statusCounts,
            TotalCount = totalCount,
            TotalPages = totalPages,
            IsAdmin = isAdmin,
            UnreadMessagesByDealId = unreadMap
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        Guid id,
        string scope = "incoming",
        string? source = null,
        string? search = null,
        string? status = null,
        int page = 1,
        int pageSize = 20,
        string? realtorSearch = null,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = User.IsInRole("Admin");
        var isRealtor = User.IsInRole("Realtor");
        var normalizedScope = NormalizeScope(scope, isAdmin, isRealtor);
        var normalizedSource = NormalizeSource(source);

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 10, 100);

        var workflowResult = await _dealRequestsApiClient.GetByIdAsync(id, cancellationToken);
        if (IsUnauthorized(workflowResult.Error))
        {
            return RedirectToLoginCurrent();
        }

        var deal = workflowResult.Data;
        var apiError = GetTempApiError() ?? workflowResult.Error;

        ApiClientResult<Web.Models.Dashboard.DashboardRequirementViewModel>? requirementResult = null;
        ApiClientResult<Web.Models.Properties.PropertySummaryViewModel>? propertyResult = null;
        ApiClientResult<IReadOnlyList<DealRequestRealtorCandidateViewModel>>? realtorsResult = null;
        ApiClientResult<IReadOnlyList<DealDocumentViewModel>>? documentsResult = null;

        if (deal is not null)
        {
            Task<ApiClientResult<Web.Models.Dashboard.DashboardRequirementViewModel>>? requirementTask = null;
            Task<ApiClientResult<Web.Models.Properties.PropertySummaryViewModel>>? propertyTask = null;
            Task<ApiClientResult<IReadOnlyList<DealRequestRealtorCandidateViewModel>>>? realtorsTask = null;
            Task<ApiClientResult<IReadOnlyList<DealDocumentViewModel>>>? documentsTask = null;

            var tasks = new List<Task>();
            documentsTask = _dealRequestsApiClient.GetDocumentsAsync(deal.Id, cancellationToken);
            tasks.Add(documentsTask);

            if (deal.ClientRequirementId.HasValue)
            {
                requirementTask = _dealRequestsApiClient.GetRequirementByIdAsync(
                    deal.ClientRequirementId.Value,
                    cancellationToken);
                tasks.Add(requirementTask);
            }

            if (deal.PropertyId.HasValue)
            {
                propertyTask = _propertiesApiClient.GetByIdAsync(deal.PropertyId.Value, cancellationToken);
                tasks.Add(propertyTask);
            }

            if (isAdmin)
            {
                realtorsTask = _dealRequestsApiClient.SearchRealtorsAsync(realtorSearch, 20, cancellationToken);
                tasks.Add(realtorsTask);
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks);
            }

            requirementResult = requirementTask?.Result;
            propertyResult = propertyTask?.Result;
            realtorsResult = realtorsTask?.Result;
            documentsResult = documentsTask?.Result;

            if (IsUnauthorized(requirementResult?.Error)
                || IsUnauthorized(propertyResult?.Error)
                || IsUnauthorized(realtorsResult?.Error)
                || IsUnauthorized(documentsResult?.Error))
            {
                return RedirectToLoginCurrent();
            }

            apiError ??= requirementResult?.Error
                ?? propertyResult?.Error
                ?? realtorsResult?.Error
                ?? documentsResult?.Error;
        }

        return View(new DealRequestDetailsViewModel
        {
            SuccessMessage = TempData[SuccessKey]?.ToString(),
            ApiError = apiError,
            Deal = deal,
            Requirement = requirementResult?.Data,
            Property = propertyResult?.Data,
            Documents = documentsResult?.Data ?? [],
            RealtorCandidates = realtorsResult?.Data ?? [],
            RealtorSearch = realtorSearch,
            IsAdmin = isAdmin,
            IsRealtor = isRealtor,
            ReturnScope = normalizedScope,
            ReturnSource = normalizedSource,
            ReturnSearch = search,
            ReturnStatus = status,
            ReturnPage = page,
            ReturnPageSize = pageSize
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Realtor")]
    public async Task<IActionResult> Accept(
        Guid id,
        string scope = "incoming",
        string? source = null,
        string? search = null,
        string? status = null,
        int page = 1,
        int pageSize = 20,
        string? realtorSearch = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _dealRequestsApiClient.AcceptAsync(id, cancellationToken);
        SetOperationResult(result, "Заявка принята.");
        return RedirectToDetails(id, scope, source, search, status, page, pageSize, realtorSearch);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Realtor")]
    public async Task<IActionResult> Reject(
        Guid id,
        string scope = "incoming",
        string? source = null,
        string? search = null,
        string? status = null,
        int page = 1,
        int pageSize = 20,
        string? realtorSearch = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _dealRequestsApiClient.RejectAsync(id, cancellationToken);
        SetOperationResult(result, "Заявка отклонена.");
        return RedirectToDetails(id, scope, source, search, status, page, pageSize, realtorSearch);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "RequireRealtorOrAdmin")]
    public async Task<IActionResult> Complete(
        Guid id,
        decimal commissionAmount,
        string commissionCurrency = "USD",
        string scope = "incoming",
        string? source = null,
        string? search = null,
        string? status = null,
        int page = 1,
        int pageSize = 20,
        string? realtorSearch = null,
        CancellationToken cancellationToken = default)
    {
        if (commissionAmount <= 0)
        {
            TempData[ErrorKey] = "Комиссия должна быть больше нуля.";
            return RedirectToDetails(id, scope, source, search, status, page, pageSize, realtorSearch);
        }

        var safeCurrency = string.IsNullOrWhiteSpace(commissionCurrency)
            ? "USD"
            : commissionCurrency.Trim().ToUpperInvariant();

        if (safeCurrency.Length != 3)
        {
            TempData[ErrorKey] = "Код валюты должен состоять из 3 символов.";
            return RedirectToDetails(id, scope, source, search, status, page, pageSize, realtorSearch);
        }

        var result = await _dealRequestsApiClient.CompleteAsync(id, commissionAmount, safeCurrency, cancellationToken);
        SetOperationResult(result, "Статус сделки изменен на «Завершена».");
        return RedirectToDetails(id, scope, source, search, status, page, pageSize, realtorSearch);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "RequireRealtorOrAdmin")]
    public async Task<IActionResult> Cancel(
        Guid id,
        string scope = "incoming",
        string? source = null,
        string? search = null,
        string? status = null,
        int page = 1,
        int pageSize = 20,
        string? realtorSearch = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _dealRequestsApiClient.CancelAsync(id, cancellationToken);
        var normalizedSource = NormalizeSource(source);
        var successMessage = string.Equals(normalizedSource, "sale", StringComparison.OrdinalIgnoreCase)
            ? "Статус заявки изменен на «Отменена»."
            : "Статус сделки изменен на «Отменена».";

        SetOperationResult(result, successMessage);
        return RedirectToDetails(id, scope, source, search, status, page, pageSize, realtorSearch);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Realtor")]
    public async Task<IActionResult> Release(
        Guid id,
        string scope = "incoming",
        string? source = null,
        string? search = null,
        string? status = null,
        int page = 1,
        int pageSize = 20,
        string? realtorSearch = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _dealRequestsApiClient.ReleaseAsync(id, cancellationToken);
        SetOperationResult(result, "Заявка возвращена в общий пул.");
        return RedirectToDetails(id, scope, source, search, status, page, pageSize, realtorSearch);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> AssignRealtor(
        Guid id,
        Guid realtorId,
        string scope = "incoming",
        string? source = null,
        string? search = null,
        string? status = null,
        int page = 1,
        int pageSize = 20,
        string? realtorSearch = null,
        CancellationToken cancellationToken = default)
    {
        if (realtorId == Guid.Empty)
        {
            TempData[ErrorKey] = "Выберите риелтора для назначения.";
            return RedirectToDetails(id, scope, source, search, status, page, pageSize, realtorSearch);
        }

        var result = await _dealRequestsApiClient.AssignRealtorAsync(id, realtorId, cancellationToken);
        SetOperationResult(result, "Ответственный риелтор назначен.");
        return RedirectToDetails(id, scope, source, search, status, page, pageSize, realtorSearch);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "RequireRealtorOrAdmin")]
    public async Task<IActionResult> PublishProperty(
        Guid id,
        string scope = "incoming",
        string? source = null,
        string? search = null,
        string? status = null,
        int page = 1,
        int pageSize = 20,
        string? realtorSearch = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _dealRequestsApiClient.PublishSalePropertyAsync(id, cancellationToken);
        SetOperationResult(result, "Объект опубликован и доступен для поиска.");
        return RedirectToDetails(id, scope, source, search, status, page, pageSize, realtorSearch);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "RequireRealtorOrAdmin")]
    public async Task<IActionResult> AddNote(
        Guid id,
        string text,
        string scope = "incoming",
        string? source = null,
        string? search = null,
        string? status = null,
        int page = 1,
        int pageSize = 20,
        string? realtorSearch = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            TempData[ErrorKey] = "Текст заметки не может быть пустым.";
            return RedirectToDetails(id, scope, source, search, status, page, pageSize, realtorSearch);
        }

        var result = await _dealRequestsApiClient.AddNoteAsync(id, text.Trim(), cancellationToken);
        if (result.IsSuccess)
        {
            TempData[SuccessKey] = "Заметка добавлена.";
        }
        else
        {
            TempData[ErrorKey] = result.Error?.Message ?? "Не удалось добавить заметку.";
        }

        return RedirectToDetails(id, scope, source, search, status, page, pageSize, realtorSearch);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "RequireRealtorOrAdmin")]
    public async Task<IActionResult> UpdateNote(
        Guid id,
        Guid noteId,
        string text,
        string scope = "incoming",
        string? source = null,
        string? search = null,
        string? status = null,
        int page = 1,
        int pageSize = 20,
        string? realtorSearch = null,
        CancellationToken cancellationToken = default)
    {
        if (noteId == Guid.Empty || string.IsNullOrWhiteSpace(text))
        {
            TempData[ErrorKey] = "Проверьте данные заметки.";
            return RedirectToDetails(id, scope, source, search, status, page, pageSize, realtorSearch);
        }

        var result = await _dealRequestsApiClient.UpdateNoteAsync(id, noteId, text.Trim(), cancellationToken);
        if (result.IsSuccess)
        {
            TempData[SuccessKey] = "Заметка обновлена.";
        }
        else
        {
            TempData[ErrorKey] = result.Error?.Message ?? "Не удалось обновить заметку.";
        }

        return RedirectToDetails(id, scope, source, search, status, page, pageSize, realtorSearch);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "RequireRealtorOrAdmin")]
    public async Task<IActionResult> DeleteNote(
        Guid id,
        Guid noteId,
        string scope = "incoming",
        string? source = null,
        string? search = null,
        string? status = null,
        int page = 1,
        int pageSize = 20,
        string? realtorSearch = null,
        CancellationToken cancellationToken = default)
    {
        if (noteId == Guid.Empty)
        {
            TempData[ErrorKey] = "Не указана заметка для удаления.";
            return RedirectToDetails(id, scope, source, search, status, page, pageSize, realtorSearch);
        }

        var result = await _dealRequestsApiClient.DeleteNoteAsync(id, noteId, cancellationToken);
        SetOperationResult(result, "Заметка удалена.");
        return RedirectToDetails(id, scope, source, search, status, page, pageSize, realtorSearch);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "RequireRealtorOrAdmin")]
    public async Task<IActionResult> AddDocument(
        Guid id,
        string title,
        IFormFile? file,
        string scope = "incoming",
        string? source = null,
        string? search = null,
        string? status = null,
        int page = 1,
        int pageSize = 20,
        string? realtorSearch = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title) || file is null)
        {
            TempData[ErrorKey] = "Укажите название документа и выберите файл.";
            return RedirectToDetails(id, scope, source, search, status, page, pageSize, realtorSearch);
        }

        if (file.Length > 5 * 1024 * 1024)
        {
            TempData[ErrorKey] = "Размер документа не должен превышать 5 МБ.";
            return RedirectToDetails(id, scope, source, search, status, page, pageSize, realtorSearch);
        }

        var result = await _dealRequestsApiClient.UploadDocumentAsync(
            id,
            title.Trim(),
            file,
            cancellationToken);

        if (result.IsSuccess)
        {
            TempData[SuccessKey] = "Документ добавлен.";
        }
        else
        {
            TempData[ErrorKey] = result.Error?.Message ?? "Не удалось добавить документ.";
        }

        return RedirectToDetails(id, scope, source, search, status, page, pageSize, realtorSearch);
    }

    [HttpGet]
    [Authorize(Policy = "RequireRealtorOrAdmin")]
    public async Task<IActionResult> DownloadDocument(
        Guid id,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var result = await _dealRequestsApiClient.DownloadDocumentAsync(id, documentId, cancellationToken);
        if (!result.IsSuccess || result.Data is null)
        {
            TempData[ErrorKey] = result.Error?.Message ?? "Не удалось открыть документ.";
            return RedirectToAction(nameof(Details), new { id });
        }

        return File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "RequireRealtorOrAdmin")]
    public async Task<IActionResult> DeleteDocument(
        Guid id,
        Guid documentId,
        string scope = "incoming",
        string? source = null,
        string? search = null,
        string? status = null,
        int page = 1,
        int pageSize = 20,
        string? realtorSearch = null,
        CancellationToken cancellationToken = default)
    {
        if (documentId == Guid.Empty)
        {
            TempData[ErrorKey] = "Не указан документ для удаления.";
            return RedirectToDetails(id, scope, source, search, status, page, pageSize, realtorSearch);
        }

        var result = await _dealRequestsApiClient.DeleteDocumentAsync(id, documentId, cancellationToken);
        SetOperationResult(result, "Документ удален.");
        return RedirectToDetails(id, scope, source, search, status, page, pageSize, realtorSearch);
    }

    private static List<DealRequestStatusCountViewModel> BuildStatusCounts(
        IReadOnlyList<DealRequestStatusCountViewModel>? source)
    {
        var map = source?.ToDictionary(x => x.Status, x => x.Count, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var result = new List<DealRequestStatusCountViewModel>(OrderedStatuses.Length);

        foreach (var status in OrderedStatuses)
        {
            result.Add(new DealRequestStatusCountViewModel
            {
                Status = status,
                Count = map.TryGetValue(status, out var count) ? count : 0
            });
        }

        foreach (var extra in map
                     .Where(x => OrderedStatuses.All(s => !string.Equals(s, x.Key, StringComparison.OrdinalIgnoreCase)))
                     .OrderBy(x => x.Key))
        {
            result.Add(new DealRequestStatusCountViewModel
            {
                Status = extra.Key,
                Count = extra.Value
            });
        }

        return result;
    }

    private static List<DealRequestStatusCountViewModel> BuildStatusCountsFromItems(
        IReadOnlyList<DealRequestListItemViewModel> items)
    {
        var grouped = items
            .GroupBy(x => x.Status, StringComparer.OrdinalIgnoreCase)
            .Select(x => new DealRequestStatusCountViewModel
            {
                Status = x.Key,
                Count = x.Count()
            })
            .ToList();

        return BuildStatusCounts(grouped);
    }

    private IActionResult RedirectToDetails(
        Guid id,
        string scope,
        string? source,
        string? search,
        string? status,
        int page,
        int pageSize,
        string? realtorSearch)
    {
        return RedirectToAction(nameof(Details), new
        {
            id,
            scope,
            source = NormalizeSource(source),
            search,
            status,
            page = Math.Max(page, 1),
            pageSize = Math.Clamp(pageSize, 10, 100),
            realtorSearch
        });
    }

    private static bool IsUnauthorized(ApiErrorViewModel? error)
    {
        return error?.StatusCode == StatusCodes.Status401Unauthorized;
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

    private void SetOperationResult(ApiOperationResult result, string successMessage)
    {
        if (result.IsSuccess)
        {
            TempData[SuccessKey] = successMessage;
            return;
        }

        TempData[ErrorKey] = result.Error?.Message ?? "Не удалось выполнить операцию.";
    }

    private IActionResult RedirectToLoginCurrent()
    {
        var returnUrl = $"{Request.Path}{Request.QueryString}";
        return RedirectToAction("Login", "Auth", new { returnUrl });
    }

    private static string NormalizeScope(string? scope, bool isAdmin, bool isRealtor)
    {
        if (string.Equals(scope, "mine", StringComparison.OrdinalIgnoreCase) && (isAdmin || isRealtor))
        {
            return "mine";
        }

        return "incoming";
    }

    private static string NormalizeSource(string? source)
    {
        if (string.Equals(source, "sale", StringComparison.OrdinalIgnoreCase))
            return "sale";

        if (string.Equals(source, "purchase", StringComparison.OrdinalIgnoreCase))
            return "purchase";

        return "all";
    }

    private static string? MapSourceFilter(string normalizedSource)
    {
        return string.Equals(normalizedSource, "sale", StringComparison.OrdinalIgnoreCase)
            ? "Sale"
            : null;
    }
}
