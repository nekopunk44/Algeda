using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.Models.Analytics;
using Web.Models.Api;
using Web.Models.Dashboard;
using Web.Models.DealRequests;
using Web.Models.Properties;
using Web.Models.Realtors;
using Web.Models.RealtorEfficiency;
using Web.Services;

namespace Web.Controllers;

[Authorize(Policy = "RequireRealtorOrAdmin")]
public class AnalyticsController : Controller
{
    private readonly DealRequestsApiClient _dealRequestsApiClient;
    private readonly RealtorDashboardApiClient _realtorDashboardApiClient;
    private readonly RealtorEfficiencyApiClient _realtorEfficiencyApiClient;
    private readonly AdminDashboardApiClient _adminDashboardApiClient;
    private readonly RealtorsApiClient _realtorsApiClient;
    private readonly AnalyticsPdfExportService _pdfExportService;

    public AnalyticsController(
        DealRequestsApiClient dealRequestsApiClient,
        RealtorDashboardApiClient realtorDashboardApiClient,
        RealtorEfficiencyApiClient realtorEfficiencyApiClient,
        AdminDashboardApiClient adminDashboardApiClient,
        RealtorsApiClient realtorsApiClient,
        AnalyticsPdfExportService pdfExportService)
    {
        _dealRequestsApiClient = dealRequestsApiClient;
        _realtorDashboardApiClient = realtorDashboardApiClient;
        _realtorEfficiencyApiClient = realtorEfficiencyApiClient;
        _adminDashboardApiClient = adminDashboardApiClient;
        _realtorsApiClient = realtorsApiClient;
        _pdfExportService = pdfExportService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? tab = null,
        string? dateFrom = null,
        string? dateTo = null,
        string? dealType = "all",
        string? source = "all",
        Guid? realtorId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await BuildViewModel(tab, dateFrom, dateTo, dealType, source, realtorId, cancellationToken);
        if (result.RedirectToLogin)
        {
            return RedirectToAction("Login", "Auth", new { returnUrl = $"{Request.Path}{Request.QueryString}" });
        }

        return View(result.ViewModel);
    }

    [HttpGet]
    public async Task<IActionResult> ExportPdf(
        string? tab = null,
        string? dateFrom = null,
        string? dateTo = null,
        string? dealType = "all",
        string? source = "all",
        Guid? realtorId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await BuildViewModel(tab, dateFrom, dateTo, dealType, source, realtorId, cancellationToken);
        if (result.RedirectToLogin)
        {
            return RedirectToAction("Login", "Auth", new { returnUrl = $"{Request.Path}{Request.QueryString}" });
        }

        if (result.ViewModel.ApiError is not null)
        {
            return RedirectToAction(nameof(Index), new { tab, dateFrom, dateTo, dealType, source, realtorId });
        }

        var pdfBytes = _pdfExportService.Build(result.ViewModel);
        var fileName = $"analytics-{result.ViewModel.ActiveTab}-{DateTime.Now:yyyyMMdd-HHmm}.pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }

    private async Task<(AnalyticsIndexViewModel ViewModel, bool RedirectToLogin)> BuildViewModel(
        string? tab,
        string? dateFrom,
        string? dateTo,
        string? dealType,
        string? source,
        Guid? realtorId,
        CancellationToken cancellationToken)
    {
        var isAdmin = User.IsInRole("Admin");
        var activeTab = AnalyticsTabs.Normalize(tab);
        if (!isAdmin && activeTab != AnalyticsTabs.Realtors)
        {
            activeTab = AnalyticsTabs.Realtors;
        }

        var from = ParseDate(dateFrom);
        var to = ParseDate(dateTo);
        var normalizedDealType = NormalizeDealType(dealType);
        var normalizedSource = NormalizeSource(source);

        var workflowDealsResult = isAdmin
            ? await _dealRequestsApiClient.GetAllAsync(500, null, null, normalizedSource == "all" ? null : normalizedSource, cancellationToken)
            : await _dealRequestsApiClient.GetMineAsync(500, null, null, normalizedSource == "all" ? null : normalizedSource, cancellationToken);
        var propertiesResult = await _realtorDashboardApiClient.GetPropertiesAsync(500, cancellationToken);
        var financialDealsResult = isAdmin
            ? await _realtorDashboardApiClient.GetDealsAsync(500, cancellationToken)
            : ApiClientResult<IReadOnlyList<DashboardDealViewModel>>.Success([]);

        if (IsUnauthorized(workflowDealsResult.Error)
            || IsUnauthorized(propertiesResult.Error)
            || IsUnauthorized(financialDealsResult.Error))
        {
            return (new AnalyticsIndexViewModel(), true);
        }

        var apiError = workflowDealsResult.Error ?? propertiesResult.Error ?? financialDealsResult.Error;
        var workflowDeals = ApplyDealFilters(workflowDealsResult.Data ?? [], from, to, normalizedDealType);
        var properties = propertiesResult.Data ?? [];
        var financialDeals = ApplyFinancialDealDateFilter(financialDealsResult.Data ?? [], from, to);

        var availableSources = (workflowDealsResult.Data ?? [])
            .Select(x => x.Source)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var agency = BuildAgencyAnalytics(workflowDeals, financialDeals);
        var propertyAnalytics = BuildPropertyAnalytics(workflowDeals, properties, financialDeals);

        QualityAnalyticsViewModel quality;
        if (isAdmin)
        {
            var complaintsResult = await _adminDashboardApiClient.GetComplaintsAsync(500, "All", null, null, cancellationToken);
            if (IsUnauthorized(complaintsResult.Error))
            {
                return (new AnalyticsIndexViewModel(), true);
            }

            apiError ??= complaintsResult.Error;
            quality = BuildQualityAnalytics(
                complaintsResult.Data ?? [],
                workflowDealsResult.Data ?? [],
                from,
                to,
                normalizedDealType,
                normalizedSource);
            agency = WithComplaintCount(
                agency,
                quality.ConfirmedComplaints + quality.PartiallyConfirmedComplaints);
        }
        else
        {
            quality = new QualityAnalyticsViewModel();
        }

        var realtorPanel = await BuildRealtorPanel(isAdmin, realtorId, cancellationToken);
        if (realtorPanel.RedirectToLogin)
        {
            return (new AnalyticsIndexViewModel(), true);
        }
        apiError ??= realtorPanel.ApiError;

        var viewModel = new AnalyticsIndexViewModel
        {
            ApiError = apiError,
            IsAdmin = isAdmin,
            ActiveTab = activeTab,
            Filters = new AnalyticsFilterViewModel
            {
                DateFrom = from,
                DateTo = to,
                DealType = normalizedDealType,
                Source = normalizedSource,
                AvailableSources = availableSources
            },
            Agency = agency,
            Realtors = realtorPanel.Panel,
            Properties = propertyAnalytics,
            Quality = quality
        };

        return (viewModel, false);
    }

    private async Task<(RealtorAnalyticsPanelViewModel Panel, ApiErrorViewModel? ApiError, bool RedirectToLogin)> BuildRealtorPanel(
        bool isAdmin,
        Guid? requestedRealtorId,
        CancellationToken cancellationToken)
    {
        if (isAdmin)
        {
            var realtorsResult = await _realtorsApiClient.GetAsync(500, cancellationToken);
            if (IsUnauthorized(realtorsResult.Error))
            {
                return (new RealtorAnalyticsPanelViewModel(), null, true);
            }

            var realtors = realtorsResult.Data ?? [];
            var selectedRealtorId = requestedRealtorId.HasValue && requestedRealtorId.Value != Guid.Empty
                ? requestedRealtorId
                : realtors.FirstOrDefault()?.Id;

            RealtorScoreSnapshotViewModel? latest = null;
            IReadOnlyList<RealtorScoreSnapshotViewModel> history = [];
            RealtorFeedbackSummaryViewModel? summary = null;
            ApiErrorViewModel? adminApiError = realtorsResult.Error;

            if (selectedRealtorId.HasValue)
            {
                var latestTask = _realtorEfficiencyApiClient.GetLatestScoreAsync(selectedRealtorId.Value, cancellationToken);
                var historyTask = _realtorEfficiencyApiClient.GetScoreHistoryAsync(selectedRealtorId.Value, 30, cancellationToken);
                var summaryTask = _realtorEfficiencyApiClient.GetFeedbackSummaryAsync(selectedRealtorId.Value, 30, cancellationToken);
                await Task.WhenAll(latestTask, historyTask, summaryTask);

                if (IsUnauthorized(latestTask.Result.Error) || IsUnauthorized(historyTask.Result.Error) || IsUnauthorized(summaryTask.Result.Error))
                {
                    return (new RealtorAnalyticsPanelViewModel(), null, true);
                }

                latest = latestTask.Result.Data;
                history = historyTask.Result.Data ?? [];
                summary = summaryTask.Result.Data;
                adminApiError ??= latestTask.Result.Error ?? historyTask.Result.Error ?? summaryTask.Result.Error;
            }

            return (new RealtorAnalyticsPanelViewModel
            {
                SelectedRealtorId = selectedRealtorId,
                Realtors = realtors,
                LatestScore = latest,
                ScoreHistory = history,
                FeedbackSummary = summary
            }, adminApiError, false);
        }

        var myIdResult = await _realtorEfficiencyApiClient.GetMyRealtorIdAsync(cancellationToken);
        if (IsUnauthorized(myIdResult.Error))
        {
            return (new RealtorAnalyticsPanelViewModel(), null, true);
        }

        var latestResult = await _realtorEfficiencyApiClient.GetMyLatestScoreAsync(cancellationToken);
        var historyResult = await _realtorEfficiencyApiClient.GetMyScoreHistoryAsync(30, cancellationToken);
        var summaryResult = await _realtorEfficiencyApiClient.GetMyFeedbackSummaryAsync(30, cancellationToken);

        if (IsUnauthorized(latestResult.Error) || IsUnauthorized(historyResult.Error) || IsUnauthorized(summaryResult.Error))
        {
            return (new RealtorAnalyticsPanelViewModel(), null, true);
        }

        var apiError = myIdResult.Error ?? latestResult.Error ?? historyResult.Error ?? summaryResult.Error;
        return (new RealtorAnalyticsPanelViewModel
        {
            SelectedRealtorId = myIdResult.Data,
            Realtors = [],
            LatestScore = latestResult.Data,
            ScoreHistory = historyResult.Data ?? [],
            FeedbackSummary = summaryResult.Data
        }, apiError, false);
    }

    private static AgencyAnalyticsViewModel BuildAgencyAnalytics(
        IReadOnlyList<DealRequestListItemViewModel> workflowDeals,
        IReadOnlyList<DashboardDealViewModel> financialDeals)
    {
        var totalDeals = workflowDeals.Count;
        var completedDeals = workflowDeals.Count(x => string.Equals(x.Status, "Completed", StringComparison.OrdinalIgnoreCase));
        var cancelledDeals = workflowDeals.Count(x => string.Equals(x.Status, "Cancelled", StringComparison.OrdinalIgnoreCase));
        var conversionRate = totalDeals == 0 ? 0d : Math.Round((double)completedDeals / totalDeals * 100d, 2);

        var completedDealIds = workflowDeals
            .Where(x => string.Equals(x.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Id)
            .ToHashSet();

        var completedFinancialDeals = financialDeals
            .Where(x => completedDealIds.Contains(x.Id))
            .ToList();

        var totalCommissionUsd = completedFinancialDeals.Sum(x => x.CommissionAmount);
        var realtorPayoutUsd = completedFinancialDeals.Sum(x => x.RealtorPayoutAmount);
        var agencyNetCommissionUsd = completedFinancialDeals.Sum(x => x.AgencyNetCommissionAmount);
        var avgDuration = completedFinancialDeals
            .Where(x => x.CompletedAt.HasValue)
            .Select(x => (x.CompletedAt!.Value - x.CreatedDate).TotalDays)
            .DefaultIfEmpty(0d)
            .Average();

        var monthly = completedFinancialDeals
            .Where(x => x.CompletedAt.HasValue)
            .GroupBy(x => new DateOnly(x.CompletedAt!.Value.Year, x.CompletedAt.Value.Month, 1))
            .OrderBy(x => x.Key)
            .TakeLast(6)
            .Select(x => new MonthlyPointViewModel
            {
                Label = x.Key.ToString("MM.yyyy"),
                Value = x.Count()
            })
            .ToList();

        return new AgencyAnalyticsViewModel
        {
            TotalDeals = totalDeals,
            CompletedDeals = completedDeals,
            CancelledDeals = cancelledDeals,
            ConversionRate = conversionRate,
            TotalCommissionUsd = totalCommissionUsd,
            RealtorPayoutUsd = realtorPayoutUsd,
            AgencyNetCommissionUsd = agencyNetCommissionUsd,
            AverageDealDurationDays = Math.Round(avgDuration, 2),
            ConfirmedOrPartiallyConfirmedComplaints = 0,
            MonthlyCompletedDeals = monthly
        };
    }

    private static AgencyAnalyticsViewModel WithComplaintCount(AgencyAnalyticsViewModel vm, int value)
    {
        return new AgencyAnalyticsViewModel
        {
            TotalDeals = vm.TotalDeals,
            CompletedDeals = vm.CompletedDeals,
            CancelledDeals = vm.CancelledDeals,
            ConversionRate = vm.ConversionRate,
            TotalCommissionUsd = vm.TotalCommissionUsd,
            RealtorPayoutUsd = vm.RealtorPayoutUsd,
            AgencyNetCommissionUsd = vm.AgencyNetCommissionUsd,
            AverageDealDurationDays = vm.AverageDealDurationDays,
            ConfirmedOrPartiallyConfirmedComplaints = value,
            MonthlyCompletedDeals = vm.MonthlyCompletedDeals
        };
    }

    private static PropertyAnalyticsViewModel BuildPropertyAnalytics(
        IReadOnlyList<DealRequestListItemViewModel> workflowDeals,
        IReadOnlyList<PropertySummaryViewModel> properties,
        IReadOnlyList<DashboardDealViewModel> financialDeals)
    {
        var completedIds = workflowDeals
            .Where(x => string.Equals(x.Status, "Completed", StringComparison.OrdinalIgnoreCase) && x.PropertyId.HasValue)
            .Select(x => x.PropertyId!.Value)
            .ToHashSet();

        var soldProperties = properties.Where(x => completedIds.Contains(x.Id)).ToList();
        var soldCount = soldProperties.Count;

        var soldByType = soldProperties
            .GroupBy(x => ToPropertyTypeLabel(x.Type))
            .Select(x => new NamedCountViewModel { Name = x.Key, Count = x.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        var soldPrices = soldProperties.Select(x => x.Price).OrderBy(x => x).ToList();
        var avgPrice = soldPrices.Count == 0 ? 0m : Math.Round(soldPrices.Average(), 2);
        var medianPrice = soldPrices.Count == 0
            ? 0m
            : (soldPrices.Count % 2 == 1
                ? soldPrices[soldPrices.Count / 2]
                : Math.Round((soldPrices[soldPrices.Count / 2 - 1] + soldPrices[soldPrices.Count / 2]) / 2m, 2));

        var completedAtByPropertyId = workflowDeals
            .Where(x =>
                string.Equals(x.Status, "Completed", StringComparison.OrdinalIgnoreCase)
                && x.PropertyId.HasValue
                && x.CompletedAtUtc.HasValue)
            .GroupBy(x => x.PropertyId!.Value)
            .ToDictionary(x => x.Key, x => x.Max(d => d.CompletedAtUtc!.Value));

        var saleDurationDays = soldProperties
            .Select(property =>
            {
                var soldAtUtc = property.SoldAtUtc
                                ?? (completedAtByPropertyId.TryGetValue(property.Id, out var completedAt) ? completedAt : (DateTime?)null);

                if (!soldAtUtc.HasValue)
                {
                    return (double?)null;
                }

                var createdDate = DateOnly.FromDateTime(property.CreatedDate.ToUniversalTime());
                var soldDate = DateOnly.FromDateTime(soldAtUtc.Value.ToUniversalTime());
                var days = soldDate.DayNumber - createdDate.DayNumber;

                return (double)Math.Max(days, 1);
            })
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToList();

        var avgDuration = saleDurationDays.Count == 0
            ? 0d
            : saleDurationDays.Average();

        var avgCardQuality = properties.Count == 0
            ? 0d
            : properties.Select(CalculatePropertyCardQualityScore).Average();

        return new PropertyAnalyticsViewModel
        {
            SoldObjectsCount = soldCount,
            AverageSoldPriceUsd = avgPrice,
            MedianSoldPriceUsd = medianPrice,
            AverageSaleDurationDays = Math.Round(avgDuration, 2),
            AverageCardQualityScore = Math.Round(avgCardQuality, 2),
            SoldByType = soldByType
        };
    }

    private static string ToPropertyTypeLabel(string? rawType)
    {
        if (string.IsNullOrWhiteSpace(rawType))
        {
            return "Не указано";
        }

        return PropertyValueDisplay.Type(rawType);
    }

    private static QualityAnalyticsViewModel BuildQualityAnalytics(
        IReadOnlyList<DashboardComplaintViewModel> complaints,
        IReadOnlyList<DealRequestListItemViewModel> allDeals,
        DateOnly? from,
        DateOnly? to,
        string dealType,
        string source)
    {
        var sourceByDealId = allDeals.ToDictionary(x => x.Id, x => x.Source);
        var filtered = complaints
            .Where(x => FilterByDate(x.CreatedDate, from, to))
            .Where(x => MatchDealTypeAndSource(x.DealId, sourceByDealId, dealType, source))
            .ToList();

        var byCategory = filtered
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Category) ? "Не указано" : x.Category)
            .Select(x => new NamedCountViewModel { Name = x.Key, Count = x.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        return new QualityAnalyticsViewModel
        {
            OpenComplaints = filtered.Count(x => string.Equals(x.Status, "Opened", StringComparison.OrdinalIgnoreCase)),
            InProgressComplaints = filtered.Count(x => string.Equals(x.Status, "InProgress", StringComparison.OrdinalIgnoreCase)),
            ResolvedComplaints = filtered.Count(x => string.Equals(x.Status, "Resolved", StringComparison.OrdinalIgnoreCase)),
            ConfirmedComplaints = filtered.Count(x => string.Equals(x.ModerationVerdict, "Confirmed", StringComparison.OrdinalIgnoreCase)),
            PartiallyConfirmedComplaints = filtered.Count(x => string.Equals(x.ModerationVerdict, "PartiallyConfirmed", StringComparison.OrdinalIgnoreCase)),
            NotConfirmedComplaints = filtered.Count(x => string.Equals(x.ModerationVerdict, "NotConfirmed", StringComparison.OrdinalIgnoreCase)),
            ComplaintsByCategory = byCategory
        };
    }

    private static IReadOnlyList<DealRequestListItemViewModel> ApplyDealFilters(
        IReadOnlyList<DealRequestListItemViewModel> deals,
        DateOnly? from,
        DateOnly? to,
        string dealType)
    {
        return deals
            .Where(x => FilterByDate(x.CreatedDate, from, to))
            .Where(x => MatchDealType(x.Source, dealType))
            .ToList();
    }

    private static IReadOnlyList<DashboardDealViewModel> ApplyFinancialDealDateFilter(
        IReadOnlyList<DashboardDealViewModel> deals,
        DateOnly? from,
        DateOnly? to)
    {
        return deals
            .Where(x => FilterByDate(x.CreatedDate, from, to))
            .ToList();
    }

    private static bool FilterByDate(DateTime createdDate, DateOnly? from, DateOnly? to)
    {
        var localDate = DateOnly.FromDateTime(createdDate.ToLocalTime());
        if (from.HasValue && localDate < from.Value)
            return false;
        if (to.HasValue && localDate > to.Value)
            return false;

        return true;
    }

    private static bool MatchDealType(string source, string dealType)
    {
        var isSale = string.Equals(source, "Sale", StringComparison.OrdinalIgnoreCase);
        return dealType switch
        {
            "sale" => isSale,
            "purchase" => !isSale,
            _ => true
        };
    }

    private static bool MatchDealTypeAndSource(
        Guid? dealId,
        IReadOnlyDictionary<Guid, string> sourceByDealId,
        string dealType,
        string source)
    {
        if (!dealId.HasValue || !sourceByDealId.TryGetValue(dealId.Value, out var mappedSource))
        {
            return dealType == "all" && source == "all";
        }

        if (!MatchDealType(mappedSource, dealType))
        {
            return false;
        }

        if (source != "all" && !string.Equals(mappedSource, source, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static DateOnly? ParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        return DateOnly.TryParse(raw, out var date) ? date : null;
    }

    private static string NormalizeDealType(string? raw)
    {
        return raw?.Trim().ToLowerInvariant() switch
        {
            "sale" => "sale",
            "purchase" => "purchase",
            _ => "all"
        };
    }

    private static string NormalizeSource(string? raw)
    {
        return string.IsNullOrWhiteSpace(raw)
            ? "all"
            : raw.Trim();
    }

    private static bool IsUnauthorized(ApiErrorViewModel? error)
    {
        return error?.StatusCode == StatusCodes.Status401Unauthorized;
    }

    private static double CalculatePropertyCardQualityScore(PropertySummaryViewModel property)
    {
        var ownerFields = new[]
        {
            !string.IsNullOrWhiteSpace(property.OwnerFullName),
            !string.IsNullOrWhiteSpace(property.OwnerEmail),
            !string.IsNullOrWhiteSpace(property.OwnerPhoneNumber)
        };

        var ownerScore = ownerFields.Count(x => x) / 3d * 5d;
        var photoScore = Math.Clamp(property.PhotoPaths.Count / 8d * 5d, 0d, 5d);
        var criteriaScore = Math.Clamp(property.Criteria.Count / 6d * 5d, 0d, 5d);
        return (ownerScore + photoScore + criteriaScore) / 3d;
    }
}
