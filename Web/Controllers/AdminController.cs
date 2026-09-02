using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Web.Models.Api;
using Web.Models.Dashboard;
using Web.Models.DealRequests;
using Web.Models.Realtors;
using Web.Models.RealtorEfficiency;
using Web.Services;

namespace Web.Controllers;

[Authorize(Policy = "RequireAdmin")]
public class AdminController : Controller
{
    private const string SuccessKey = "AdminSuccessMessage";
    private const string ErrorKey = "AdminErrorMessage";

    private readonly AdminDashboardApiClient _adminDashboardApiClient;
    private readonly RealtorsApiClient _realtorsApiClient;
    private readonly RealtorEfficiencyApiClient _realtorEfficiencyApiClient;
    private readonly DealChatApiClient _dealChatApiClient;
    private readonly DealRequestsApiClient _dealRequestsApiClient;

    public AdminController(
        AdminDashboardApiClient adminDashboardApiClient,
        RealtorsApiClient realtorsApiClient,
        RealtorEfficiencyApiClient realtorEfficiencyApiClient,
        DealChatApiClient dealChatApiClient,
        DealRequestsApiClient dealRequestsApiClient)
    {
        _adminDashboardApiClient = adminDashboardApiClient;
        _realtorsApiClient = realtorsApiClient;
        _realtorEfficiencyApiClient = realtorEfficiencyApiClient;
        _dealChatApiClient = dealChatApiClient;
        _dealRequestsApiClient = dealRequestsApiClient;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return RedirectToAction(nameof(SystemSettings));
    }

    [HttpGet]
    public IActionResult SystemHealth()
    {
        return RedirectToAction(nameof(SystemSettings));
    }

    [HttpGet]
    public async Task<IActionResult> SystemSettings(CancellationToken cancellationToken = default)
    {
        ViewData["AdminActive"] = "SystemSettings";

        var eligibilityTask = _realtorEfficiencyApiClient.GetEligibilitySettingsAsync(cancellationToken);
        var commissionsTask = _realtorEfficiencyApiClient.GetCommissionSettingsAsync(cancellationToken);
        var levelSettingsTask = _realtorEfficiencyApiClient.GetLevelSettingsAsync(cancellationToken);
        await Task.WhenAll(eligibilityTask, commissionsTask, levelSettingsTask);

        var eligibilityResult = eligibilityTask.Result;
        var commissionsResult = commissionsTask.Result;
        var levelSettingsResult = levelSettingsTask.Result;

        if (IsUnauthorized(eligibilityResult.Error) || IsUnauthorized(commissionsResult.Error) || IsUnauthorized(levelSettingsResult.Error))
        {
            return RedirectToLoginCurrent();
        }

        return View(new AdminSystemSettingsViewModel
        {
            SuccessMessage = TempData[SuccessKey]?.ToString(),
            ApiError = GetTempApiError() ?? eligibilityResult.Error ?? commissionsResult.Error ?? levelSettingsResult.Error,
            EligibilitySettings = eligibilityResult.Data ?? new RealtorEligibilitySettingsViewModel(),
            CommissionSettings = commissionsResult.Data ?? new RealtorCommissionSettingsViewModel(),
            LevelSettings = levelSettingsResult.Data ?? new RealtorLevelSettingsViewModel()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Criteria(
        int page = 1,
        int pageSize = 20,
        string? search = null,
        string sort = "name_asc",
        bool includeHidden = true,
        CancellationToken cancellationToken = default)
    {
        ViewData["AdminActive"] = "Criteria";

        var criteriaPageTask = _adminDashboardApiClient.GetCriteriaPagedAsync(
            page,
            pageSize,
            includeHidden,
            search,
            sort,
            cancellationToken);
        var criteriaListTask = _adminDashboardApiClient.GetCriteriaAsync(
            limit: 500,
            includeHidden: true,
            cancellationToken: cancellationToken);

        await Task.WhenAll(criteriaPageTask, criteriaListTask);

        var criteriaResult = criteriaPageTask.Result;
        var criteriaListResult = criteriaListTask.Result;

        if (IsUnauthorized(criteriaResult.Error) || IsUnauthorized(criteriaListResult.Error))
        {
            return RedirectToLoginCurrent();
        }

        var categories = (criteriaListResult.Data ?? [])
            .Select(x => x.Category?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
            .Cast<string>()
            .ToList();

        return View(new AdminCriteriaViewModel
        {
            SuccessMessage = TempData[SuccessKey]?.ToString(),
            ApiError = GetTempApiError() ?? criteriaResult.Error ?? criteriaListResult.Error,
            CriteriaPage = criteriaResult.Data ?? new DashboardCriteriaPageViewModel
            {
                Page = Math.Max(page, 1),
                PageSize = Math.Clamp(pageSize, 1, 100)
            },
            Search = search,
            Sort = sort,
            IncludeHidden = includeHidden,
            Categories = categories,
            ExistingCriteria = criteriaListResult.Data ?? []
        });
    }

    [HttpGet]
    public async Task<IActionResult> Currencies(
        bool includeInactive = true,
        CancellationToken cancellationToken = default)
    {
        ViewData["AdminActive"] = "Currencies";

        var currenciesResult = await _adminDashboardApiClient.GetCurrenciesAsync(
            limit: 500,
            includeInactive: includeInactive,
            cancellationToken: cancellationToken);

        if (IsUnauthorized(currenciesResult.Error))
        {
            return RedirectToLoginCurrent();
        }

        return View(new AdminCurrenciesViewModel
        {
            SuccessMessage = TempData[SuccessKey]?.ToString(),
            ApiError = GetTempApiError() ?? currenciesResult.Error,
            Currencies = currenciesResult.Data ?? [],
            IncludeInactive = includeInactive
        });
    }

    [HttpGet]
    public async Task<IActionResult> DocumentAccess(
        string? dateFrom = null,
        string? dateTo = null,
        string? action = null,
        string? dealId = null,
        string? search = null,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        ViewData["AdminActive"] = "DocumentAccess";
        const int pageSize = 10;
        page = Math.Max(page, 1);

        var normalizedAction = NormalizeDocumentAction(action);
        var normalizedDealId = Guid.TryParse(dealId, out var parsedDealId) && parsedDealId != Guid.Empty
            ? parsedDealId.ToString()
            : null;

        var logTask = _dealRequestsApiClient.GetDocumentAccessLogAsync(
            dateFrom,
            dateTo,
            normalizedAction,
            normalizedDealId,
            search,
            500,
            cancellationToken);
        var dealsTask = _dealRequestsApiClient.GetAllAsync(500, cancellationToken: cancellationToken);
        var usersTask = _adminDashboardApiClient.GetUserAccessAsync(null, null, 500, false, cancellationToken);

        await Task.WhenAll(logTask, dealsTask, usersTask);

        var result = logTask.Result;
        var dealsResult = dealsTask.Result;
        var usersResult = usersTask.Result;

        if (IsUnauthorized(result.Error) || IsUnauthorized(dealsResult.Error) || IsUnauthorized(usersResult.Error))
        {
            return RedirectToLoginCurrent();
        }

        var dealsById = (dealsResult.Data ?? []).ToDictionary(x => x.Id, x => x);
        var usersById = (usersResult.Data ?? []).ToDictionary(x => x.UserId, x => x);
        var allItems = (result.Data ?? [])
            .Select(item => EnrichDocumentLog(item, dealsById, usersById))
            .ToList();
        var totalCount = allItems.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Min(page, totalPages);

        return View(new AdminDealDocumentAccessLogViewModel
        {
            SuccessMessage = TempData[SuccessKey]?.ToString(),
            ApiError = GetTempApiError() ?? result.Error ?? dealsResult.Error,
            Items = allItems
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            DateFrom = dateFrom,
            DateTo = dateTo,
            Action = normalizedAction,
            DealId = normalizedDealId ?? dealId,
            Search = search
        });
    }

    [HttpGet]
    public async Task<IActionResult> RealtorAnalytics(
        Guid? realtorId = null,
        string? search = null,
        string? level = null,
        int realtorPage = 1,
        int feedbackPage = 1,
        int scorePage = 1,
        CancellationToken cancellationToken = default)
    {
        ViewData["AdminActive"] = "RealtorAnalytics";

        var realtorsResult = await _realtorsApiClient.GetAsync(500, cancellationToken);
        var realtorRequestsResult = await _adminDashboardApiClient.GetRealtorRequestsAsync("All", 500, cancellationToken);
        if (IsUnauthorized(realtorsResult.Error))
        {
            return RedirectToLoginCurrent();
        }
        if (IsUnauthorized(realtorRequestsResult.Error))
        {
            return RedirectToLoginCurrent();
        }

        var realtorEmailLookup = BuildRealtorEmailLookup(realtorRequestsResult.Data ?? []);
        var normalizedSearch = search?.Trim();
        var realtors = (realtorsResult.Data ?? [])
            .Select(x => new RealtorCardViewModel
            {
                Id = x.Id,
                FirstName = x.FirstName,
                LastName = x.LastName,
                MiddleName = x.MiddleName,
                PhoneNumber = x.PhoneNumber,
                Email = realtorEmailLookup.TryGetValue(BuildRealtorIdentityKey(x.FirstName, x.LastName, x.MiddleName, x.PhoneNumber), out var email) ? email : x.Email,
                AverageRating = x.AverageRating,
                Level = x.Level,
                IsLevelManuallyAssigned = x.IsLevelManuallyAssigned,
                DealsThisMonth = x.DealsThisMonth
            })
            .Where(x => MatchesRealtorSearch(x, normalizedSearch))
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .ToList();

        var normalizedLevel = NormalizeRealtorLevel(level);
        if (!string.IsNullOrWhiteSpace(normalizedLevel))
        {
            realtors = realtors
                .Where(x => string.Equals(x.Level, normalizedLevel, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        const int pageSize = 10;
        var realtorsTotalCount = realtors.Count;
        var realtorsTotalPages = Math.Max(1, (int)Math.Ceiling(realtorsTotalCount / (double)pageSize));
        realtorPage = Math.Clamp(realtorPage, 1, realtorsTotalPages);

        if (realtorId.HasValue && realtorId.Value != Guid.Empty)
        {
            var selectedIndex = realtors.FindIndex(x => x.Id == realtorId.Value);
            if (selectedIndex >= 0)
            {
                realtorPage = selectedIndex / pageSize + 1;
            }
        }

        var pagedRealtors = realtors
            .Skip((realtorPage - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var selectedRealtorId = realtorId.HasValue && realtorId.Value != Guid.Empty
            ? realtorId
            : null;

        ApiErrorViewModel? apiError = GetTempApiError() ?? realtorsResult.Error ?? realtorRequestsResult.Error;
        RealtorScoreSnapshotViewModel? latestScore = null;
        IReadOnlyList<RealtorScoreSnapshotViewModel> scoreHistory = [];
        IReadOnlyList<RealtorScoreSnapshotViewModel> fullScoreHistory = [];
        var scoreHistoryTotalCount = 0;
        var scoreHistoryTotalPages = 1;
        int openComplaintsCountForRealtor = 0;
        RealtorFeedbackSummaryViewModel? feedbackSummary = null;
        var feedbackTotalCount = 0;
        var feedbackTotalPages = 1;
        RealtorComplaintStatsViewModel complaintStats = new();

        if (selectedRealtorId.HasValue)
        {
            var safeHistoryLimit = 100;

            var latestTask = _realtorEfficiencyApiClient.GetLatestScoreAsync(selectedRealtorId.Value, cancellationToken);
            var historyTask = _realtorEfficiencyApiClient.GetScoreHistoryAsync(selectedRealtorId.Value, safeHistoryLimit, cancellationToken);
            var feedbackSummaryTask = _realtorEfficiencyApiClient.GetFeedbackSummaryAsync(selectedRealtorId.Value, safeHistoryLimit, cancellationToken);
            var complaintsTask = _adminDashboardApiClient.GetComplaintsAsync(500, cancellationToken: cancellationToken);

            await Task.WhenAll(latestTask, historyTask, feedbackSummaryTask, complaintsTask);

            if (IsUnauthorized(latestTask.Result.Error)
                || IsUnauthorized(historyTask.Result.Error)
                || IsUnauthorized(feedbackSummaryTask.Result.Error)
                || IsUnauthorized(complaintsTask.Result.Error))
            {
                return RedirectToLoginCurrent();
            }

            latestScore = latestTask.Result.Data;
            fullScoreHistory = historyTask.Result.Data ?? [];
            scoreHistoryTotalCount = fullScoreHistory.Count;
            scoreHistoryTotalPages = Math.Max(1, (int)Math.Ceiling(scoreHistoryTotalCount / (double)pageSize));
            scorePage = Math.Clamp(scorePage, 1, scoreHistoryTotalPages);
            scoreHistory = fullScoreHistory
                .OrderByDescending(x => x.CreatedDate)
                .Skip((scorePage - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            feedbackSummary = feedbackSummaryTask.Result.Data;
            feedbackTotalCount = feedbackSummary?.RecentItems.Count ?? 0;
            feedbackTotalPages = Math.Max(1, (int)Math.Ceiling(feedbackTotalCount / (double)pageSize));
            feedbackPage = Math.Clamp(feedbackPage, 1, feedbackTotalPages);
            feedbackSummary = PageFeedbackSummary(feedbackSummary, feedbackPage, pageSize);

            var realtorComplaints = (complaintsTask.Result.Data ?? [])
                .Where(x => x.TargetRealtorId == selectedRealtorId.Value)
                .OrderByDescending(x => x.CreatedDate)
                .ToList();

            openComplaintsCountForRealtor = realtorComplaints
                .Count(x => string.Equals(x.Status, "Opened", StringComparison.OrdinalIgnoreCase));
            complaintStats = BuildComplaintStats(realtorComplaints);

            apiError ??= latestTask.Result.Error
                         ?? historyTask.Result.Error
                         ?? feedbackSummaryTask.Result.Error
                         ?? complaintsTask.Result.Error;
        }

        var selectedRealtor = realtors.FirstOrDefault(x => x.Id == selectedRealtorId);

        return View(new AdminRealtorAnalyticsViewModel
        {
            SuccessMessage = TempData[SuccessKey]?.ToString(),
            ApiError = apiError,
            Realtors = pagedRealtors,
            RealtorsPage = realtorPage,
            RealtorsTotalPages = realtorsTotalPages,
            RealtorsTotalCount = realtorsTotalCount,
            Search = normalizedSearch,
            Level = normalizedLevel,
            SelectedRealtorId = selectedRealtorId,
            SelectedRealtor = selectedRealtor,
            LatestScore = latestScore,
            ScoreHistory = scoreHistory,
            ScoreHistoryPage = scorePage,
            ScoreHistoryTotalPages = scoreHistoryTotalPages,
            ScoreHistoryTotalCount = scoreHistoryTotalCount,
            BlockReasons = [],
            ClientTrustTrendDelta = CalculateTrendDelta(fullScoreHistory, x => x.ClientTrustScore),
            AdminPerformanceTrendDelta = CalculateTrendDelta(fullScoreHistory, x => x.AdminPerformanceScore),
            OpenComplaintsCountForRealtor = openComplaintsCountForRealtor,
            ComplaintStats = complaintStats,
            FeedbackSummary = feedbackSummary,
            FeedbackPage = feedbackPage,
            FeedbackTotalPages = feedbackTotalPages,
            FeedbackTotalCount = feedbackTotalCount,
            EligibilitySettings = null
        });
    }

    [HttpGet]
    public IActionResult RealtorCommissions()
    {
        return RedirectToAction(nameof(SystemSettings));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRealtorCommissions(
        string[] levels,
        decimal[] percents,
        CancellationToken cancellationToken = default)
    {
        var safeLevels = levels ?? [];
        var safePercents = percents ?? [];
        var count = Math.Min(safeLevels.Length, safePercents.Length);
        if (count <= 0)
        {
            TempData[ErrorKey] = "Не удалось прочитать настройки комиссий.";
            return RedirectToAction(nameof(SystemSettings));
        }

        var items = Enumerable.Range(0, count)
            .Select(i => new RealtorLevelCommissionViewModel
            {
                Level = safeLevels[i],
                Percent = safePercents[i]
            })
            .ToList();

        var result = await _realtorEfficiencyApiClient.UpdateCommissionSettingsAsync(items, cancellationToken);
        SetOperationResult(result, "Настройки комиссий сохранены.");
        return RedirectToAction(nameof(SystemSettings));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRealtorLevelMode(
        Guid realtorId,
        bool isLevelManuallyAssigned,
        string? manualLevel,
        string? search = null,
        string? level = null,
        int realtorPage = 1,
        int feedbackPage = 1,
        int scorePage = 1,
        CancellationToken cancellationToken = default)
    {
        if (realtorId == Guid.Empty)
        {
            TempData[ErrorKey] = "Не указан риелтор.";
            return RedirectToAction(nameof(RealtorAnalytics), new { search, level, realtorPage, feedbackPage, scorePage });
        }

        var normalizedManualLevel = NormalizeRealtorLevel(manualLevel) ?? "Junior";
        var result = await _realtorsApiClient.UpdateLevelModeAsync(
            realtorId,
            isLevelManuallyAssigned,
            isLevelManuallyAssigned ? normalizedManualLevel : null,
            cancellationToken);

        SetOperationResult(result, isLevelManuallyAssigned
            ? "Уровень риелтора зафиксирован вручную."
            : "Автоматическая смена уровня включена.");

        return RedirectToAction(nameof(RealtorAnalytics), new { realtorId, search, level, realtorPage, feedbackPage, scorePage });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateEligibilitySettings(
        bool restrictionsEnabled,
        string[] tierNames,
        decimal[] tierMinPrices,
        decimal?[] tierMaxPrices,
        CancellationToken cancellationToken = default)
    {
        var safeTierNames = tierNames ?? [];
        var safeTierMaxPrices = tierMaxPrices ?? [];
        var count = safeTierNames.Length;

        if (count <= 0)
        {
            TempData[ErrorKey] = "Не удалось прочитать уровни ограничений.";
            return RedirectToAction(nameof(SystemSettings));
        }

        var tiers = Enumerable.Range(0, count)
            .Select(i => new RealtorEligibilityTierViewModel
            {
                Name = safeTierNames[i],
                MinPrice = 0m,
                MaxPrice = string.Equals(safeTierNames[i], "Top", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : i < safeTierMaxPrices.Length ? safeTierMaxPrices[i] : null,
                MinClientTrustScore = 0,
                MinAdminPerformanceScore = 0,
                SortOrder = i
            })
            .ToList();

        var result = await _realtorEfficiencyApiClient.UpdateEligibilitySettingsAsync(
            new RealtorEligibilitySettingsViewModel
            {
                RestrictionsEnabled = restrictionsEnabled,
                MinConfirmedHistoryDeals = 0,
                CriticalComplaintLookbackDays = 365,
                ScoreSnapshotMaxAgeHours = 24,
                BlockOnCriticalComplaints = false,
                PriceTiers = tiers
            },
            cancellationToken);

        SetOperationResult(result, "Настройки ограничений сохранены.");
        return RedirectToAction(nameof(SystemSettings));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRealtorLevelSettings(
        double demotionBuffer,
        string[] levels,
        int[] minCompletedDeals,
        double[] minClientTrustScores,
        double[] minAdminPerformanceScores,
        CancellationToken cancellationToken = default)
    {
        var safeLevels = levels ?? [];
        var safeMinCompletedDeals = minCompletedDeals ?? [];
        var safeMinClientTrustScores = minClientTrustScores ?? [];
        var safeMinAdminPerformanceScores = minAdminPerformanceScores ?? [];
        var count = new[]
        {
            safeLevels.Length,
            safeMinCompletedDeals.Length,
            safeMinClientTrustScores.Length,
            safeMinAdminPerformanceScores.Length
        }.Min();

        if (count <= 0)
        {
            TempData[ErrorKey] = "Не удалось прочитать правила уровней.";
            return RedirectToAction(nameof(SystemSettings));
        }

        var rules = Enumerable.Range(0, count)
            .Select(i => new RealtorLevelRuleViewModel
            {
                Level = safeLevels[i],
                MinCompletedDeals = safeMinCompletedDeals[i],
                MinClientTrustScore = safeMinClientTrustScores[i],
                MinAdminPerformanceScore = safeMinAdminPerformanceScores[i],
                SortOrder = i
            })
            .ToList();

        var result = await _realtorEfficiencyApiClient.UpdateLevelSettingsAsync(
            new RealtorLevelSettingsViewModel
            {
                DemotionBuffer = demotionBuffer,
                Rules = rules
            },
            cancellationToken);

        SetOperationResult(result, "Правила уровней риелторов сохранены.");
        return RedirectToAction(nameof(SystemSettings));
    }

    [HttpGet]
    public async Task<IActionResult> Clients(
        string? search = null,
        int limit = 300,
        CancellationToken cancellationToken = default)
    {
        ViewData["AdminActive"] = "Clients";

        var clientsResult = await _adminDashboardApiClient.GetClientsAsync(limit, cancellationToken);
        if (IsUnauthorized(clientsResult.Error))
        {
            return RedirectToLoginCurrent();
        }

        var clients = clientsResult.Data ?? [];
        if (!string.IsNullOrWhiteSpace(search))
        {
            var needle = search.Trim();
            clients = clients
                .Where(x =>
                    x.FullName.Contains(needle, StringComparison.OrdinalIgnoreCase)
                    || x.PhoneNumber.Contains(needle, StringComparison.OrdinalIgnoreCase)
                    || (x.Email != null && x.Email.Contains(needle, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        return View(new AdminClientsViewModel
        {
            SuccessMessage = TempData[SuccessKey]?.ToString(),
            ApiError = GetTempApiError() ?? clientsResult.Error,
            Clients = clients
                .OrderByDescending(x => x.CreatedDate)
                .ToList(),
            Search = search
        });
    }

    [HttpGet]
    public async Task<IActionResult> Access(
        string? search = null,
        string? role = null,
        int limit = 200,
        CancellationToken cancellationToken = default)
    {
        ViewData["AdminActive"] = "Access";

        var usersResult = await _adminDashboardApiClient.GetUserAccessAsync(search, role, limit, false, cancellationToken);
        if (IsUnauthorized(usersResult.Error))
        {
            return RedirectToLoginCurrent();
        }

        return View(new AdminAccessViewModel
        {
            SuccessMessage = TempData[SuccessKey]?.ToString(),
            ApiError = GetTempApiError() ?? usersResult.Error,
            Users = usersResult.Data ?? [],
            Search = search,
            Role = role,
            IsCurrentUserSuperAdmin = User.IsInRole("SuperAdmin"),
            CurrentUserId = GetCurrentUserId()
        });
    }

    [HttpGet]
    public async Task<IActionResult> RealtorRequests(
        string? status = "Pending",
        string? search = null,
        int limit = 150,
        CancellationToken cancellationToken = default)
    {
        ViewData["AdminActive"] = "RealtorRequests";

        var requestsResult = await _adminDashboardApiClient.GetRealtorRequestsAsync(status, limit, cancellationToken);
        if (IsUnauthorized(requestsResult.Error))
        {
            return RedirectToLoginCurrent();
        }

        var normalizedSearch = search?.Trim();
        return View(new AdminRealtorRequestsViewModel
        {
            SuccessMessage = TempData[SuccessKey]?.ToString(),
            ApiError = GetTempApiError() ?? requestsResult.Error,
            Requests = (requestsResult.Data ?? [])
                .Where(x => MatchesRealtorRequestSearch(x, normalizedSearch))
                .OrderByDescending(x => x.CreatedDate)
                .ToList(),
            Status = status,
            Search = normalizedSearch
        });
    }

    [HttpGet]
    public async Task<IActionResult> Complaints(
        string? status = "All",
        string? category = null,
        string? link = null,
        string? dealId = null,
        string? client = null,
        string? realtor = null,
        string? dateFrom = null,
        string? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        ViewData["AdminActive"] = "Complaints";

        var normalizedStatus = string.IsNullOrWhiteSpace(status)
            ? "All"
            : status.Trim();
        var apiStatus = string.Equals(normalizedStatus, "All", StringComparison.OrdinalIgnoreCase)
            ? null
            : normalizedStatus;

        var complaintsTask = _adminDashboardApiClient.GetComplaintsAsync(
            300,
            apiStatus,
            category,
            ParseDealLinkFilter(link),
            cancellationToken);
        var clientsTask = _adminDashboardApiClient.GetClientsAsync(500, cancellationToken);
        var realtorsTask = _realtorsApiClient.GetAsync(500, cancellationToken);
        var realtorRequestsTask = _adminDashboardApiClient.GetRealtorRequestsAsync("All", 500, cancellationToken);
        await Task.WhenAll(complaintsTask, clientsTask, realtorsTask, realtorRequestsTask);

        var complaintsResult = complaintsTask.Result;
        var clientsResult = clientsTask.Result;
        var realtorsResult = realtorsTask.Result;

        if (IsUnauthorized(complaintsResult.Error)
            || IsUnauthorized(clientsResult.Error)
            || IsUnauthorized(realtorsResult.Error)
            || IsUnauthorized(realtorRequestsTask.Result.Error))
        {
            return RedirectToLoginCurrent();
        }

        var realtorEmailLookup = BuildRealtorEmailLookup(realtorRequestsTask.Result.Data ?? []);
        var enrichedComplaints = EnrichComplaints(
            complaintsResult.Data ?? [],
            clientsResult.Data ?? [],
            realtorsResult.Data ?? [],
            realtorEmailLookup);

        if (!string.IsNullOrWhiteSpace(dealId))
        {
            var dealNeedle = dealId.Trim();
            enrichedComplaints = enrichedComplaints
                .Where(x => x.DealId.HasValue && x.DealId.Value.ToString().Contains(dealNeedle, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(client))
        {
            var clientNeedle = client.Trim();
            enrichedComplaints = enrichedComplaints
                .Where(x =>
                    x.ClientId.ToString().Contains(clientNeedle, StringComparison.OrdinalIgnoreCase)
                    || x.ClientFullName.Contains(clientNeedle, StringComparison.OrdinalIgnoreCase)
                    || (x.ClientPhoneNumber?.Contains(clientNeedle, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (x.ClientEmail?.Contains(clientNeedle, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(realtor))
        {
            var realtorNeedle = realtor.Trim();
            enrichedComplaints = enrichedComplaints
                .Where(x =>
                    (x.TargetRealtorId?.ToString().Contains(realtorNeedle, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (!string.IsNullOrWhiteSpace(x.RealtorFullName)
                        && x.RealtorFullName.Contains(realtorNeedle, StringComparison.OrdinalIgnoreCase))
                    || (x.RealtorPhoneNumber?.Contains(realtorNeedle, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (x.RealtorEmail?.Contains(realtorNeedle, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();
        }

        var fromDate = ParseDateOnlyOrNull(dateFrom);
        var toDate = ParseDateOnlyOrNull(dateTo);
        if (fromDate.HasValue)
        {
            enrichedComplaints = enrichedComplaints
                .Where(x => DateOnly.FromDateTime(x.CreatedDate.ToLocalTime()) >= fromDate.Value)
                .ToList();
        }

        if (toDate.HasValue)
        {
            enrichedComplaints = enrichedComplaints
                .Where(x => DateOnly.FromDateTime(x.CreatedDate.ToLocalTime()) <= toDate.Value)
                .ToList();
        }

        return View(new AdminComplaintsViewModel
        {
            SuccessMessage = TempData[SuccessKey]?.ToString(),
            ApiError = GetTempApiError()
                       ?? complaintsResult.Error
                       ?? clientsResult.Error
                       ?? realtorsResult.Error
                       ?? realtorRequestsTask.Result.Error,
            FilterStatus = normalizedStatus,
            FilterCategory = category,
            FilterLink = link,
            FilterDealId = dealId,
            FilterClientQuery = client,
            FilterRealtorQuery = realtor,
            FilterDateFrom = fromDate?.ToString("yyyy-MM-dd") ?? dateFrom,
            FilterDateTo = toDate?.ToString("yyyy-MM-dd") ?? dateTo,
            Complaints = enrichedComplaints
                .OrderByDescending(x => x.CreatedDate)
                .ToList(),
            OpenComplaints = []
        });
    }

    [HttpGet]
    public async Task<IActionResult> ComplaintDetails(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        ViewData["AdminActive"] = "Complaints";

        if (id == Guid.Empty)
        {
            TempData[ErrorKey] = "Не указана жалоба.";
            return RedirectToAction(nameof(Complaints));
        }

        var complaintTask = _adminDashboardApiClient.GetComplaintByIdAsync(id, cancellationToken);
        var clientsTask = _adminDashboardApiClient.GetClientsAsync(500, cancellationToken);
        var realtorsTask = _realtorsApiClient.GetAsync(500, cancellationToken);
        var realtorRequestsTask = _adminDashboardApiClient.GetRealtorRequestsAsync("All", 500, cancellationToken);
        await Task.WhenAll(complaintTask, clientsTask, realtorsTask, realtorRequestsTask);

        if (IsUnauthorized(complaintTask.Result.Error)
            || IsUnauthorized(clientsTask.Result.Error)
            || IsUnauthorized(realtorsTask.Result.Error)
            || IsUnauthorized(realtorRequestsTask.Result.Error))
        {
            return RedirectToLoginCurrent();
        }

        var realtorEmailLookup = BuildRealtorEmailLookup(realtorRequestsTask.Result.Data ?? []);
        var complaints = EnrichComplaints(
            complaintTask.Result.Data is null ? [] : [complaintTask.Result.Data],
            clientsTask.Result.Data ?? [],
            realtorsTask.Result.Data ?? [],
            realtorEmailLookup);

        var complaint = complaints.FirstOrDefault();
        if (complaint is not null
            && complaint.DealId.HasValue
            && (complaint.PropertyId is null
                || string.IsNullOrWhiteSpace(complaint.RealtorPhoneNumber)
                || string.IsNullOrWhiteSpace(complaint.RealtorEmail)))
        {
            var dealResult = await _dealRequestsApiClient.GetByIdAsync(complaint.DealId.Value, cancellationToken);
            if (IsUnauthorized(dealResult.Error))
            {
                return RedirectToLoginCurrent();
            }

            if (dealResult.Data is not null)
            {
                complaint = MergeComplaintWithDeal(complaint, dealResult.Data);
            }
        }

        return View(new AdminComplaintDetailsViewModel
        {
            SuccessMessage = TempData[SuccessKey]?.ToString(),
            ApiError = GetTempApiError()
                       ?? complaintTask.Result.Error
                       ?? clientsTask.Result.Error
                       ?? realtorsTask.Result.Error
                       ?? realtorRequestsTask.Result.Error,
            Complaint = complaint
        });
    }

    [HttpGet]
    public async Task<IActionResult> DealChats(
        string? dealId = null,
        string? client = null,
        string? realtor = null,
        CancellationToken cancellationToken = default)
    {
        ViewData["AdminActive"] = "DealChats";

        var parsedDealId = TryParseGuidOrNull(dealId);
        var parsedClientId = TryParseGuidOrNull(client);
        var parsedRealtorId = TryParseGuidOrNull(realtor);

        var chatsTask = _dealChatApiClient.GetAdminChatsAsync(
            parsedDealId,
            parsedClientId,
            parsedRealtorId,
            200,
            cancellationToken);
        var clientsTask = _adminDashboardApiClient.GetClientsAsync(500, cancellationToken);
        var realtorsTask = _realtorsApiClient.GetAsync(500, cancellationToken);
        var realtorRequestsTask = _adminDashboardApiClient.GetRealtorRequestsAsync("All", 500, cancellationToken);
        var dealsTask = _dealRequestsApiClient.GetAllAsync(500, cancellationToken: cancellationToken);

        await Task.WhenAll(chatsTask, clientsTask, realtorsTask, realtorRequestsTask, dealsTask);

        var chatsResult = chatsTask.Result;
        var clientsResult = clientsTask.Result;
        var realtorsResult = realtorsTask.Result;
        var realtorRequestsResult = realtorRequestsTask.Result;
        var dealsResult = dealsTask.Result;

        if (IsUnauthorized(chatsResult.Error))
        {
            return RedirectToLoginCurrent();
        }
        if (IsUnauthorized(clientsResult.Error)
            || IsUnauthorized(realtorsResult.Error)
            || IsUnauthorized(realtorRequestsResult.Error)
            || IsUnauthorized(dealsResult.Error))
        {
            return RedirectToLoginCurrent();
        }

        var realtorEmailLookup = BuildRealtorEmailLookup(realtorRequestsResult.Data ?? []);

        var clientsById = (clientsResult.Data ?? []).ToDictionary(x => x.Id, x => x);
        var realtorsById = (realtorsResult.Data ?? []).ToDictionary(x => x.Id, x => x);
        var dealsById = (dealsResult.Data ?? []).ToDictionary(x => x.Id, x => x);

        var items = (chatsResult.Data ?? [])
            .Select(x =>
            {
                clientsById.TryGetValue(x.ClientId, out var clientInfo);
                realtorsById.TryGetValue(x.RealtorId, out var realtorInfo);
                dealsById.TryGetValue(x.DealId, out var dealInfo);

                var clientName = clientInfo?.FullName ?? x.ClientName;
                var clientPhone = clientInfo?.PhoneNumber;
                var clientEmail = clientInfo?.Email;

                var realtorName = realtorInfo?.FullName ?? x.RealtorName;
                var realtorPhone = realtorInfo?.PhoneNumber;
                var realtorEmailKey = BuildRealtorIdentityKey(
                    realtorInfo?.FirstName,
                    realtorInfo?.LastName,
                    realtorInfo?.MiddleName,
                    realtorInfo?.PhoneNumber);

                var realtorEmail = realtorEmailLookup.TryGetValue(realtorEmailKey, out var email)
                    ? email
                    : realtorInfo?.Email;

                return new Web.Models.DealChat.DealChatSummaryViewModel
                {
                    DealId = x.DealId,
                    PropertyTitle = dealInfo?.PropertyTitle,
                    ClientId = x.ClientId,
                    ClientName = clientName,
                    ClientPhoneNumber = clientPhone,
                    ClientEmail = clientEmail,
                    RealtorId = x.RealtorId,
                    RealtorName = realtorName,
                    RealtorPhoneNumber = realtorPhone,
                    RealtorEmail = realtorEmail,
                    DealStatus = x.DealStatus,
                    LastMessageAtUtc = x.LastMessageAtUtc,
                    LastMessagePreview = x.LastMessagePreview,
                    MessageCount = x.MessageCount
                };
            })
            .ToList();

        if (!string.IsNullOrWhiteSpace(dealId))
        {
            var dealNeedle = dealId.Trim();
            items = items
                .Where(x => x.DealId.ToString().Contains(dealNeedle, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(client))
        {
            var clientNeedle = client.Trim();
            items = items
                .Where(x =>
                    x.ClientId.ToString().Contains(clientNeedle, StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrWhiteSpace(x.ClientName) && x.ClientName.Contains(clientNeedle, StringComparison.OrdinalIgnoreCase))
                    || (x.ClientPhoneNumber?.Contains(clientNeedle, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (x.ClientEmail?.Contains(clientNeedle, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(realtor))
        {
            var realtorNeedle = realtor.Trim();
            items = items
                .Where(x =>
                    x.RealtorId.ToString().Contains(realtorNeedle, StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrWhiteSpace(x.RealtorName) && x.RealtorName.Contains(realtorNeedle, StringComparison.OrdinalIgnoreCase))
                    || (x.RealtorPhoneNumber?.Contains(realtorNeedle, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (x.RealtorEmail?.Contains(realtorNeedle, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();
        }

        return View(new AdminDealChatsViewModel
        {
            SuccessMessage = TempData[SuccessKey]?.ToString(),
            ApiError = GetTempApiError() ?? chatsResult.Error ?? clientsResult.Error ?? realtorsResult.Error ?? realtorRequestsResult.Error ?? dealsResult.Error,
            FilterDealId = dealId,
            FilterClientQuery = client,
            FilterRealtorQuery = realtor,
            Items = items
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateClient(
        string firstName,
        string lastName,
        string? middleName,
        string phoneNumber,
        string? email,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(firstName)
            || string.IsNullOrWhiteSpace(lastName)
            || string.IsNullOrWhiteSpace(phoneNumber))
        {
            TempData[ErrorKey] = "Заполните имя, фамилию и телефон клиента.";
            return RedirectToAction(nameof(Clients));
        }

        var result = await _adminDashboardApiClient.CreateClientAsync(
            firstName.Trim(),
            lastName.Trim(),
            string.IsNullOrWhiteSpace(middleName) ? null : middleName.Trim(),
            phoneNumber.Trim(),
            string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            cancellationToken);

        SetOperationResult(result, "Клиент успешно создан.");
        return RedirectToAction(nameof(Clients));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateClient(
        Guid clientId,
        string phoneNumber,
        CancellationToken cancellationToken)
    {
        if (clientId == Guid.Empty || string.IsNullOrWhiteSpace(phoneNumber))
        {
            TempData[ErrorKey] = "Укажите клиента и телефон.";
            return RedirectToAction(nameof(Clients));
        }

        var result = await _adminDashboardApiClient.UpdateClientAsync(clientId, phoneNumber.Trim(), cancellationToken);
        SetOperationResult(result, "Данные клиента обновлены.");
        return RedirectToAction(nameof(Clients));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteClient(Guid clientId, CancellationToken cancellationToken)
    {
        if (clientId == Guid.Empty)
        {
            TempData[ErrorKey] = "Не указан клиент для удаления.";
            return RedirectToAction(nameof(Clients));
        }

        var result = await _adminDashboardApiClient.DeleteClientAsync(clientId, cancellationToken);
        SetOperationResult(result, "Клиент удален.");
        return RedirectToAction(nameof(Clients));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCriterion(
        string code,
        string displayName,
        string valueType,
        string? category,
        string? description,
        bool isHidden,
        string? optionsText,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code)
            || string.IsNullOrWhiteSpace(displayName)
            || string.IsNullOrWhiteSpace(valueType))
        {
            TempData[ErrorKey] = "Заполните код, название и тип значения критерия.";
            return RedirectToAction(nameof(Criteria));
        }

        var parseResult = ParseCriterionOptions(optionsText);
        if (!parseResult.IsSuccess)
        {
            TempData[ErrorKey] = parseResult.ErrorMessage;
            return RedirectToAction(nameof(Criteria));
        }

        var result = await _adminDashboardApiClient.CreateCriterionAsync(
            code.Trim(),
            displayName.Trim(),
            valueType.Trim(),
            string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
            string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            isHidden,
            parseResult.Options,
            cancellationToken);

        SetOperationResult(result, "Критерий создан.");
        return RedirectToAction(nameof(Criteria));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCriterion(
        Guid criterionId,
        string code,
        string displayName,
        string valueType,
        string? category,
        string? description,
        bool isHidden,
        string? optionsText,
        CancellationToken cancellationToken)
    {
        if (criterionId == Guid.Empty
            || string.IsNullOrWhiteSpace(code)
            || string.IsNullOrWhiteSpace(displayName)
            || string.IsNullOrWhiteSpace(valueType))
        {
            TempData[ErrorKey] = "Проверьте обязательные поля при редактировании критерия.";
            return RedirectToAction(nameof(Criteria));
        }

        var parseResult = ParseCriterionOptions(optionsText);
        if (!parseResult.IsSuccess)
        {
            TempData[ErrorKey] = parseResult.ErrorMessage;
            return RedirectToAction(nameof(Criteria));
        }

        var result = await _adminDashboardApiClient.UpdateCriterionAsync(
            criterionId,
            code.Trim(),
            displayName.Trim(),
            valueType.Trim(),
            string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
            string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            isHidden,
            parseResult.Options,
            cancellationToken);

        SetOperationResult(result, "Критерий обновлен.");
        return RedirectToAction(nameof(Criteria));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetCriterionHidden(
        Guid criterionId,
        bool isHidden,
        int page = 1,
        int pageSize = 20,
        string? search = null,
        string sort = "name_asc",
        bool includeHidden = true,
        CancellationToken cancellationToken = default)
    {
        if (criterionId == Guid.Empty)
        {
            TempData[ErrorKey] = "Не указан критерий.";
            return RedirectToAction(nameof(Criteria), new
            {
                page = Math.Max(page, 1),
                pageSize = Math.Clamp(pageSize, 1, 100),
                search,
                sort,
                includeHidden
            });
        }

        var result = await _adminDashboardApiClient.SetCriterionHiddenAsync(criterionId, isHidden, cancellationToken);
        SetOperationResult(result, isHidden ? "Критерий скрыт." : "Критерий снова отображается.");
        return RedirectToAction(nameof(Criteria), new
        {
            page = Math.Max(page, 1),
            pageSize = Math.Clamp(pageSize, 1, 100),
            search,
            sort,
            includeHidden
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCriterion(Guid criterionId, CancellationToken cancellationToken)
    {
        if (criterionId == Guid.Empty)
        {
            TempData[ErrorKey] = "Не указан критерий для удаления.";
            return RedirectToAction(nameof(Criteria));
        }

        var result = await _adminDashboardApiClient.DeleteCriterionAsync(criterionId, cancellationToken);
        SetOperationResult(result, "Критерий удален.");
        return RedirectToAction(nameof(Criteria));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCurrency(
        string code,
        string name,
        string symbol,
        decimal rateToBase,
        bool isActive,
        bool includeInactive = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code)
            || string.IsNullOrWhiteSpace(name)
            || string.IsNullOrWhiteSpace(symbol)
            || rateToBase <= 0)
        {
            TempData[ErrorKey] = "Заполните корректно поля валюты.";
            return RedirectToAction(nameof(Currencies), new { includeInactive });
        }

        var result = await _adminDashboardApiClient.CreateCurrencyAsync(
            code.Trim().ToUpperInvariant(),
            name.Trim(),
            symbol.Trim(),
            rateToBase,
            isActive,
            cancellationToken);

        SetOperationResult(result, "Валюта создана.");
        return RedirectToAction(nameof(Currencies), new { includeInactive });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCurrency(
        Guid currencyId,
        string code,
        string name,
        string symbol,
        decimal rateToBase,
        bool isActive,
        bool includeInactive = true,
        CancellationToken cancellationToken = default)
    {
        if (currencyId == Guid.Empty
            || string.IsNullOrWhiteSpace(code)
            || string.IsNullOrWhiteSpace(name)
            || string.IsNullOrWhiteSpace(symbol)
            || rateToBase <= 0)
        {
            TempData[ErrorKey] = "Проверьте данные валюты перед обновлением.";
            return RedirectToAction(nameof(Currencies), new { includeInactive });
        }

        var result = await _adminDashboardApiClient.UpdateCurrencyAsync(
            currencyId,
            code.Trim().ToUpperInvariant(),
            name.Trim(),
            symbol.Trim(),
            rateToBase,
            isActive,
            cancellationToken);

        SetOperationResult(result, "Валюта обновлена.");
        return RedirectToAction(nameof(Currencies), new { includeInactive });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetCurrencyActive(
        Guid currencyId,
        bool isActive,
        bool includeInactive = true,
        CancellationToken cancellationToken = default)
    {
        if (currencyId == Guid.Empty)
        {
            TempData[ErrorKey] = "Не указана валюта.";
            return RedirectToAction(nameof(Currencies), new { includeInactive });
        }

        var result = await _adminDashboardApiClient.SetCurrencyActiveAsync(currencyId, isActive, cancellationToken);
        SetOperationResult(result, isActive ? "Валюта активирована." : "Валюта деактивирована.");
        return RedirectToAction(nameof(Currencies), new { includeInactive });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCurrency(
        Guid currencyId,
        bool includeInactive = true,
        CancellationToken cancellationToken = default)
    {
        if (currencyId == Guid.Empty)
        {
            TempData[ErrorKey] = "Не указана валюта для удаления.";
            return RedirectToAction(nameof(Currencies), new { includeInactive });
        }

        var result = await _adminDashboardApiClient.DeleteCurrencyAsync(currencyId, cancellationToken);
        SetOperationResult(result, "Валюта удалена.");
        return RedirectToAction(nameof(Currencies), new { includeInactive });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignRole(
        Guid userId,
        string role,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(role))
        {
            TempData[ErrorKey] = "Не указан пользователь или роль.";
            return RedirectToAction(nameof(Access));
        }

        var result = await _adminDashboardApiClient.AssignRoleAsync(userId, role, cancellationToken);
        SetOperationResult(result, $"Роль {role} назначена.");
        return RedirectToAction(nameof(Access));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FreezeAccount(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            TempData[ErrorKey] = "Не указан пользователь.";
            return RedirectToAction(nameof(Access));
        }

        var result = await _adminDashboardApiClient.FreezeAccountAsync(userId, cancellationToken);
        SetOperationResult(result, "Аккаунт заморожен.");
        return RedirectToAction(nameof(Access));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnfreezeAccount(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            TempData[ErrorKey] = "Не указан пользователь.";
            return RedirectToAction(nameof(Access));
        }

        var result = await _adminDashboardApiClient.UnfreezeAccountAsync(userId, cancellationToken);
        SetOperationResult(result, "Аккаунт разморожен.");
        return RedirectToAction(nameof(Access));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveRole(
        Guid userId,
        string role,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(role))
        {
            TempData[ErrorKey] = "Не указан пользователь или роль.";
            return RedirectToAction(nameof(Access));
        }

        var result = await _adminDashboardApiClient.RemoveRoleAsync(userId, role, cancellationToken);
        SetOperationResult(result, $"Роль {role} снята.");
        return RedirectToAction(nameof(Access));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TransferSuperAdmin(
        Guid userId,
        string password,
        bool confirmed,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(password) || !confirmed)
        {
            TempData[ErrorKey] = "Подтвердите передачу прав и введите пароль.";
            return RedirectToAction(nameof(Access));
        }

        var result = await _adminDashboardApiClient.TransferSuperAdminAsync(userId, password, confirmed, cancellationToken);
        SetOperationResult(result, "Права главного администратора переданы.");
        if (result.IsSuccess)
        {
            await RefreshCurrentUserAsRegularAdmin();
        }

        return RedirectToAction(nameof(Access));
    }

    private async Task RefreshCurrentUserAsRegularAdmin()
    {
        var authentication = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var identity = new ClaimsIdentity(
            User.Claims.Where(claim =>
                claim.Type != ClaimTypes.Role || !string.Equals(claim.Value, "SuperAdmin", StringComparison.OrdinalIgnoreCase)),
            CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            authentication.Properties);
    }

    private Guid GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var userId) ? userId : Guid.Empty;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveRealtorRequest(
        Guid requestId,
        string? comment,
        CancellationToken cancellationToken)
    {
        if (requestId == Guid.Empty)
        {
            TempData[ErrorKey] = "Не указана заявка риелтора.";
            return RedirectToAction(nameof(RealtorRequests));
        }

        var result = await _adminDashboardApiClient.ApproveRealtorRequestAsync(requestId, comment, cancellationToken);
        SetOperationResult(result, "Заявка риелтора одобрена.");
        return RedirectToAction(nameof(RealtorRequests), new { status = "Pending" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectRealtorRequest(
        Guid requestId,
        string? comment,
        CancellationToken cancellationToken)
    {
        if (requestId == Guid.Empty)
        {
            TempData[ErrorKey] = "Не указана заявка риелтора.";
            return RedirectToAction(nameof(RealtorRequests));
        }

        var result = await _adminDashboardApiClient.RejectRealtorRequestAsync(requestId, comment, cancellationToken);
        SetOperationResult(result, "Заявка риелтора отклонена.");
        return RedirectToAction(nameof(RealtorRequests), new { status = "Pending" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkComplaintInProgress(
        Guid complaintId,
        bool returnToDetails = false,
        CancellationToken cancellationToken = default)
    {
        if (complaintId == Guid.Empty)
        {
            TempData[ErrorKey] = "Не указана жалоба.";
            return RedirectToAction(nameof(Complaints));
        }

        var result = await _adminDashboardApiClient.MarkComplaintInProgressAsync(complaintId, cancellationToken);
        SetOperationResult(result, "Жалоба переведена в статус «В работе».");
        return returnToDetails
            ? RedirectToAction(nameof(ComplaintDetails), new { id = complaintId })
            : RedirectToAction(nameof(Complaints));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkComplaintOpened(
        Guid complaintId,
        bool returnToDetails = false,
        CancellationToken cancellationToken = default)
    {
        if (complaintId == Guid.Empty)
        {
            TempData[ErrorKey] = "Не указана жалоба.";
            return RedirectToAction(nameof(Complaints));
        }

        var result = await _adminDashboardApiClient.MarkComplaintOpenedAsync(complaintId, cancellationToken);
        SetOperationResult(result, "Жалоба переведена в статус «Открыта».");
        return returnToDetails
            ? RedirectToAction(nameof(ComplaintDetails), new { id = complaintId })
            : RedirectToAction(nameof(Complaints));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolveComplaint(
        Guid complaintId,
        string verdict,
        string resolution,
        bool returnToDetails = false,
        CancellationToken cancellationToken = default)
    {
        if (complaintId == Guid.Empty || string.IsNullOrWhiteSpace(verdict) || string.IsNullOrWhiteSpace(resolution))
        {
            TempData[ErrorKey] = "Укажите жалобу, вердикт и текст решения.";
            return RedirectToAction(nameof(Complaints));
        }

        var result = await _adminDashboardApiClient.ResolveComplaintAsync(
            complaintId,
            verdict.Trim(),
            resolution.Trim(),
            cancellationToken);

        SetOperationResult(result, "Жалоба решена.");
        return returnToDetails
            ? RedirectToAction(nameof(ComplaintDetails), new { id = complaintId })
            : RedirectToAction(nameof(Complaints));
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

        TempData[ErrorKey] = result.Error?.Message ?? "Операцию не удалось выполнить.";
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

    private static RealtorComplaintStatsViewModel BuildComplaintStats(
        IReadOnlyList<DashboardComplaintViewModel> complaints)
    {
        return new RealtorComplaintStatsViewModel
        {
            TotalCount = complaints.Count,
            OpenCount = complaints.Count(x => string.Equals(x.Status, "Opened", StringComparison.OrdinalIgnoreCase)),
            InProgressCount = complaints.Count(x => string.Equals(x.Status, "InProgress", StringComparison.OrdinalIgnoreCase)),
            ResolvedCount = complaints.Count(x => string.Equals(x.Status, "Resolved", StringComparison.OrdinalIgnoreCase)),
            ConfirmedCount = complaints.Count(x => string.Equals(x.ModerationVerdict, "Confirmed", StringComparison.OrdinalIgnoreCase)),
            PartiallyConfirmedCount = complaints.Count(x => string.Equals(x.ModerationVerdict, "PartiallyConfirmed", StringComparison.OrdinalIgnoreCase)),
            NotConfirmedCount = complaints.Count(x => string.Equals(x.ModerationVerdict, "NotConfirmed", StringComparison.OrdinalIgnoreCase)),
            RecentComplaints = complaints
                .OrderByDescending(x => x.CreatedDate)
                .Take(5)
                .ToList()
        };
    }

    private static RealtorFeedbackSummaryViewModel? PageFeedbackSummary(
        RealtorFeedbackSummaryViewModel? summary,
        int page,
        int pageSize)
    {
        if (summary is null)
            return null;

        return new RealtorFeedbackSummaryViewModel
        {
            RealtorId = summary.RealtorId,
            TotalFeedbackCount = summary.TotalFeedbackCount,
            ServiceFeedbackCount = summary.ServiceFeedbackCount,
            PropertyFeedbackCount = summary.PropertyFeedbackCount,
            PurchaseFeedbackCount = summary.PurchaseFeedbackCount,
            SaleFeedbackCount = summary.SaleFeedbackCount,
            AverageServiceScore = summary.AverageServiceScore,
            AverageCommunicationScore = summary.AverageCommunicationScore,
            AverageResponsivenessScore = summary.AverageResponsivenessScore,
            AverageExpertiseScore = summary.AverageExpertiseScore,
            AverageTitleAccuracyScore = summary.AverageTitleAccuracyScore,
            AverageDescriptionAccuracyScore = summary.AverageDescriptionAccuracyScore,
            AveragePhotosAccuracyScore = summary.AveragePhotosAccuracyScore,
            AverageCriteriaAccuracyScore = summary.AverageCriteriaAccuracyScore,
            RecentItems = summary.RecentItems
                .OrderByDescending(x => x.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList()
        };
    }

    private static IReadOnlyList<RealtorBlockReasonViewModel> BuildBlockReasons(
        RealtorCardViewModel? selectedRealtor,
        RealtorEligibilitySettingsViewModel? eligibilitySettings)
    {
        if (eligibilitySettings is null || !eligibilitySettings.RestrictionsEnabled)
        {
            return
            [
                new RealtorBlockReasonViewModel
                {
                    Code = "LEVEL_RESTRICTIONS_DISABLED",
                    Title = "Ограничения выключены",
                    Description = "Риелторы могут брать заявки независимо от уровня и цены объекта.",
                    IsBlockingNow = false
                }
            ];
        }

        return eligibilitySettings.PriceTiers
            .OrderBy(x => x.SortOrder)
            .Select(tier => new RealtorBlockReasonViewModel
            {
                Code = $"LEVEL_{tier.Name.ToUpperInvariant()}",
                Title = $"{DashboardValueDisplay.RealtorLevel(tier.Name)}: {FormatPriceRange(tier)}",
                Description = "Риелтор может брать заявки только из диапазона своего уровня.",
                IsBlockingNow = selectedRealtor is not null
                    && !string.Equals(selectedRealtor.Level, tier.Name, StringComparison.OrdinalIgnoreCase)
            })
            .ToList();
    }

    private static string FormatPriceRange(RealtorEligibilityTierViewModel tier)
    {
        return tier.MaxPrice.HasValue
            ? $"{tier.MinPrice:0.##} - {tier.MaxPrice.Value:0.##}"
            : $"от {tier.MinPrice:0.##}";
    }

    private static double? CalculateTrendDelta(
        IReadOnlyList<RealtorScoreSnapshotViewModel> history,
        Func<RealtorScoreSnapshotViewModel, double> selector)
    {
        if (history.Count < 2)
        {
            return null;
        }

        var ordered = history.OrderBy(x => x.CreatedDate).ToList();
        return selector(ordered[^1]) - selector(ordered[0]);
    }

    private static bool? ParseDealLinkFilter(string? rawValue)
    {
        return rawValue?.Trim().ToLowerInvariant() switch
        {
            "linked" => true,
            "general" => false,
            _ => null
        };
    }

    private static DateOnly? ParseDateOnlyOrNull(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        return DateOnly.TryParse(rawValue.Trim(), out var parsed)
            ? parsed
            : null;
    }

    private static Guid? TryParseGuidOrNull(string? rawValue)
    {
        if (Guid.TryParse(rawValue, out var parsed) && parsed != Guid.Empty)
        {
            return parsed;
        }

        return null;
    }

    private static string? NormalizeRealtorLevel(string? rawValue)
    {
        return rawValue?.Trim() switch
        {
            "Junior" => "Junior",
            "Standard" => "Standard",
            "Top" => "Top",
            _ => null
        };
    }

    private static string? NormalizeDocumentAction(string? rawValue)
    {
        return rawValue?.Trim() switch
        {
            "Added" => "Added",
            "Opened" => "Opened",
            "Deleted" => "Deleted",
            _ => null
        };
    }

    private static DealDocumentAccessLogItemViewModel EnrichDocumentLog(
        DealDocumentAccessLogItemViewModel item,
        IReadOnlyDictionary<Guid, DealRequestListItemViewModel> dealsById,
        IReadOnlyDictionary<Guid, DashboardIdentityUserViewModel> usersById)
    {
        dealsById.TryGetValue(item.DealId, out var deal);

        var actorDisplayName = item.ActorDisplayName;
        var actorEmail = item.ActorEmail;
        if (item.ActorUserId.HasValue && usersById.TryGetValue(item.ActorUserId.Value, out var user))
        {
             if (!string.IsNullOrWhiteSpace(user.DisplayName)) actorDisplayName = user.DisplayName;
             if (string.IsNullOrWhiteSpace(item.ActorEmail)) actorEmail = user.Email;
        }

        return new DealDocumentAccessLogItemViewModel
        {
            Id = item.Id,
            DealId = item.DealId,
            DealDocumentId = item.DealDocumentId,
            DocumentTitle = item.DocumentTitle,
            DocumentFileName = item.DocumentFileName,
            Action = item.Action,
            ActorUserId = item.ActorUserId,
            ActorDisplayName = actorDisplayName,
            ActorEmail = actorEmail,
            ActorRole = item.ActorRole,
            IpAddress = item.IpAddress,
            UserAgent = item.UserAgent,
            CreatedDate = item.CreatedDate,
            DealSummary = item.DealSummary,
            ClientSummary = item.ClientSummary,
            RealtorSummary = item.RealtorSummary,
            PropertyTitle = deal?.PropertyTitle,
            DealStatus = deal?.Status ?? "Undefined",
            ClientFullName = deal?.ClientFullName,
            ClientPhoneNumber = deal?.ClientPhoneNumber,
            ClientEmail = deal?.ClientEmail
        };
    }

    private static bool MatchesRealtorSearch(RealtorCardViewModel realtor, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var fields = string.Join(' ', new[]
        {
            realtor.FullName,
            realtor.LastName,
            realtor.FirstName,
            realtor.MiddleName,
            realtor.PhoneNumber,
            realtor.Email,
            realtor.Id.ToString()
        }.Where(x => !string.IsNullOrWhiteSpace(x)));

        var phoneDigits = OnlyDigits(realtor.PhoneNumber);
        var tokens = search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return tokens.All(token =>
        {
            if (fields.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var tokenDigits = OnlyDigits(token);
            return tokenDigits.Length > 0 && phoneDigits.Contains(tokenDigits, StringComparison.Ordinal);
        });
    }

    private static bool MatchesRealtorRequestSearch(DashboardRealtorRegistrationRequestViewModel request, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var fields = string.Join(' ', new[]
        {
            request.FullName,
            request.LastName,
            request.FirstName,
            request.MiddleName,
            request.PhoneNumber,
            request.Email,
            request.Id.ToString(),
            request.IdentityUserId?.ToString()
        }.Where(x => !string.IsNullOrWhiteSpace(x)));

        var phoneDigits = OnlyDigits(request.PhoneNumber);
        var tokens = search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return tokens.All(token =>
        {
            if (fields.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var tokenDigits = OnlyDigits(token);
            return tokenDigits.Length > 0 && phoneDigits.Contains(tokenDigits, StringComparison.Ordinal);
        });
    }

    private static string OnlyDigits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value.Where(char.IsDigit).ToArray());
    }

    private static Dictionary<string, string> BuildRealtorEmailLookup(
        IReadOnlyList<DashboardRealtorRegistrationRequestViewModel> requests)
    {
        var dictionary = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var request in requests.Where(x => string.Equals(x.Status, "Approved", StringComparison.OrdinalIgnoreCase)))
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                continue;
            }

            var key = BuildRealtorIdentityKey(
                request.FirstName,
                request.LastName,
                request.MiddleName,
                request.PhoneNumber);

            if (!dictionary.ContainsKey(key))
            {
                dictionary[key] = request.Email.Trim();
            }
        }

        return dictionary;
    }

    private static string BuildRealtorIdentityKey(
        string? firstName,
        string? lastName,
        string? middleName,
        string? phoneNumber)
    {
        return $"{NormalizeSearchPart(lastName)}|{NormalizeSearchPart(firstName)}|{NormalizeSearchPart(middleName)}|{NormalizeSearchPart(phoneNumber)}";
    }

    private static string NormalizeSearchPart(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }

    private static List<DashboardComplaintViewModel> EnrichComplaints(
        IReadOnlyList<DashboardComplaintViewModel> complaints,
        IReadOnlyList<DashboardClientViewModel> clients,
        IReadOnlyList<RealtorCardViewModel> realtors,
        IReadOnlyDictionary<string, string>? realtorEmailLookup = null)
    {
        var clientsById = clients.ToDictionary(x => x.Id, x => x);
        var realtorsById = realtors.ToDictionary(x => x.Id, x => x);

        return complaints
            .Select(x =>
            {
                clientsById.TryGetValue(x.ClientId, out var client);

                RealtorCardViewModel? realtor = null;
                if (x.TargetRealtorId.HasValue)
                {
                    realtorsById.TryGetValue(x.TargetRealtorId.Value, out realtor);
                }

                var realtorEmailKey = BuildRealtorIdentityKey(
                    realtor?.FirstName,
                    realtor?.LastName,
                    realtor?.MiddleName,
                    realtor?.PhoneNumber);
                var mappedRealtorEmail = realtor?.Email;
                if (string.IsNullOrWhiteSpace(mappedRealtorEmail)
                    && realtorEmailLookup is not null
                    && realtorEmailLookup.TryGetValue(realtorEmailKey, out var lookupEmail))
                {
                    mappedRealtorEmail = lookupEmail;
                }

                return new DashboardComplaintViewModel
                {
                    Id = x.Id,
                    ClientId = x.ClientId,
                    TargetRealtorId = x.TargetRealtorId,
                    DealId = x.DealId,
                    PropertyId = x.PropertyId,
                    ClientFullName = client?.FullName ?? x.ClientId.ToString(),
                    ClientPhoneNumber = client?.PhoneNumber,
                    ClientEmail = client?.Email,
                    RealtorFullName = realtor?.FullName,
                    RealtorPhoneNumber = realtor?.PhoneNumber,
                    RealtorEmail = mappedRealtorEmail,
                    Category = x.Category,
                    Subject = x.Subject,
                    Description = StripComplaintContextSuffix(x.Description),
                    Status = x.Status,
                    ModerationVerdict = x.ModerationVerdict,
                    AdminResolution = x.AdminResolution,
                    ResolvedAt = x.ResolvedAt,
                    CreatedDate = x.CreatedDate
                };
            })
            .ToList();
    }

    private static string StripComplaintContextSuffix(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return description;
        }

        const string marker = "[Контекст:";
        var markerIndex = description.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return description;
        }

        return description[..markerIndex].TrimEnd();
    }

    private static DashboardComplaintViewModel MergeComplaintWithDeal(
        DashboardComplaintViewModel complaint,
        DealRequestListItemViewModel deal)
    {
        var targetRealtorId = complaint.TargetRealtorId ?? deal.RealtorId;

        return new DashboardComplaintViewModel
        {
            Id = complaint.Id,
            ClientId = complaint.ClientId,
            TargetRealtorId = targetRealtorId,
            DealId = complaint.DealId,
            PropertyId = complaint.PropertyId ?? deal.PropertyId,
            ClientFullName = complaint.ClientFullName,
            ClientPhoneNumber = complaint.ClientPhoneNumber,
            ClientEmail = complaint.ClientEmail,
            RealtorFullName = string.IsNullOrWhiteSpace(complaint.RealtorFullName)
                ? deal.RealtorFullName
                : complaint.RealtorFullName,
            RealtorPhoneNumber = string.IsNullOrWhiteSpace(complaint.RealtorPhoneNumber)
                ? deal.RealtorPhoneNumber
                : complaint.RealtorPhoneNumber,
            RealtorEmail = string.IsNullOrWhiteSpace(complaint.RealtorEmail)
                ? deal.RealtorEmail
                : complaint.RealtorEmail,
            Category = complaint.Category,
            Subject = complaint.Subject,
            Description = complaint.Description,
            Status = complaint.Status,
            ModerationVerdict = complaint.ModerationVerdict,
            AdminResolution = complaint.AdminResolution,
            ResolvedAt = complaint.ResolvedAt,
            CreatedDate = complaint.CreatedDate
        };
    }

    private static (bool IsSuccess, List<CriterionOptionInput>? Options, string? ErrorMessage) ParseCriterionOptions(string? optionsText)
    {
        if (string.IsNullOrWhiteSpace(optionsText))
        {
            return (true, null, null);
        }

        var lines = optionsText
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToList();

        if (lines.Count == 0)
        {
            return (true, null, null);
        }

        var options = new List<CriterionOptionInput>(lines.Count);

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var parts = line.Split('|');
            if (parts.Length < 2 || parts.Length > 3)
            {
                return (false, null, $"Неверный формат опции в строке {index + 1}. Используйте: value|label|sortOrder");
            }

            var value = parts[0].Trim();
            var label = parts[1].Trim();
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(label))
            {
                return (false, null, $"Опция в строке {index + 1} должна содержать значение и метку.");
            }

            var sortOrder = index;
            if (parts.Length == 3
                && !string.IsNullOrWhiteSpace(parts[2])
                && !int.TryParse(parts[2], out sortOrder))
            {
                return (false, null, $"sortOrder в строке {index + 1} должен быть целым числом.");
            }

            if (sortOrder < 0)
            {
                return (false, null, $"sortOrder в строке {index + 1} не может быть меньше нуля.");
            }

            options.Add(new CriterionOptionInput(value, label, sortOrder));
        }

        return (true, options, null);
    }
}
