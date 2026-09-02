using Web.Models.Api;
using Web.Models.RealtorEfficiency;

namespace Web.Services;

public class RealtorEfficiencyApiClient
{
    private readonly HttpClient _httpClient;

    public RealtorEfficiencyApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<ApiOperationResult> SubmitFeedbackAsync(
        Guid dealId,
        int serviceScore,
        string formType,
        int? communicationScore,
        int? responsivenessScore,
        int? expertiseScore,
        int? titleAccuracyScore,
        int? criteriaAccuracyScore,
        int? descriptionAccuracyScore,
        int? photosAccuracyScore,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Post, "api/realtor-efficiency/feedback", new
        {
            dealId,
            serviceScore,
            formType,
            communicationScore,
            responsivenessScore,
            expertiseScore,
            titleAccuracyScore,
            criteriaAccuracyScore,
            descriptionAccuracyScore,
            photosAccuracyScore,
            comment
        }, cancellationToken);
    }

    public async Task<ApiClientResult<DealFeedbackStateViewModel>> GetFeedbackStateAsync(
        Guid dealId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"api/realtor-efficiency/feedback/deals/{dealId}/state",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<DealFeedbackStateViewModel>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<DealFeedbackStateDto>(
                ApiClientSupport.JsonOptions,
                cancellationToken);

            if (payload is null)
            {
                return ApiClientResult<DealFeedbackStateViewModel>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API returned an empty feedback state payload."));
            }

            return ApiClientResult<DealFeedbackStateViewModel>.Success(new DealFeedbackStateViewModel
            {
                DealId = payload.DealId,
                FormType = payload.FormType,
                IsCompleted = payload.IsCompleted,
                IsSubmitted = payload.IsSubmitted,
                CanSubmit = payload.CanSubmit,
                CompletedAtUtc = payload.CompletedAtUtc,
                DeadlineUtc = payload.DeadlineUtc,
                DaysRemaining = payload.DaysRemaining,
                BlockReasonCode = payload.BlockReasonCode,
                BlockReason = payload.BlockReason
            });
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<DealFeedbackStateViewModel>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<DealFeedbackStateViewModel>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public async Task<ApiClientResult<RealtorScoreSnapshotViewModel>> GetLatestScoreAsync(
        Guid realtorId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"api/realtor-efficiency/realtors/{realtorId}/scores/latest",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<RealtorScoreSnapshotViewModel>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<RealtorScoreSnapshotDto>(
                ApiClientSupport.JsonOptions,
                cancellationToken);

            if (payload is null)
            {
                return ApiClientResult<RealtorScoreSnapshotViewModel>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API returned an empty realtor score payload."));
            }

            return ApiClientResult<RealtorScoreSnapshotViewModel>.Success(MapSnapshot(payload));
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<RealtorScoreSnapshotViewModel>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<RealtorScoreSnapshotViewModel>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public async Task<ApiClientResult<Guid>> GetMyRealtorIdAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("api/realtor-efficiency/me/realtor-id", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<Guid>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<MyRealtorIdDto>(
                ApiClientSupport.JsonOptions,
                cancellationToken);

            if (payload is null || payload.RealtorId == Guid.Empty)
            {
                return ApiClientResult<Guid>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API returned an empty realtor identity payload."));
            }

            return ApiClientResult<Guid>.Success(payload.RealtorId);
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

    public async Task<ApiClientResult<RealtorScoreSnapshotViewModel>> GetMyLatestScoreAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                "api/realtor-efficiency/me/scores/latest",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<RealtorScoreSnapshotViewModel>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<RealtorScoreSnapshotDto>(
                ApiClientSupport.JsonOptions,
                cancellationToken);

            if (payload is null)
            {
                return ApiClientResult<RealtorScoreSnapshotViewModel>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API returned an empty realtor score payload."));
            }

            return ApiClientResult<RealtorScoreSnapshotViewModel>.Success(MapSnapshot(payload));
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<RealtorScoreSnapshotViewModel>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<RealtorScoreSnapshotViewModel>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public async Task<ApiClientResult<IReadOnlyList<RealtorScoreSnapshotViewModel>>> GetMyScoreHistoryAsync(
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var safeLimit = Math.Clamp(limit, 1, 200);
            var response = await _httpClient.GetAsync(
                $"api/realtor-efficiency/me/scores/history?limit={safeLimit}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<IReadOnlyList<RealtorScoreSnapshotViewModel>>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<List<RealtorScoreSnapshotDto>>(
                ApiClientSupport.JsonOptions,
                cancellationToken);

            if (payload is null)
            {
                return ApiClientResult<IReadOnlyList<RealtorScoreSnapshotViewModel>>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API returned an empty realtor score history payload."));
            }

            var mapped = payload
                .Select(MapSnapshot)
                .OrderByDescending(x => x.CreatedDate)
                .ToList();

            return ApiClientResult<IReadOnlyList<RealtorScoreSnapshotViewModel>>.Success(mapped);
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<IReadOnlyList<RealtorScoreSnapshotViewModel>>.Failure(
                ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<IReadOnlyList<RealtorScoreSnapshotViewModel>>.Failure(
                ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public async Task<ApiClientResult<RealtorFeedbackSummaryViewModel>> GetMyFeedbackSummaryAsync(
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var safeLimit = Math.Clamp(limit, 1, 100);
            var response = await _httpClient.GetAsync(
                $"api/realtor-efficiency/me/feedback-summary?limit={safeLimit}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<RealtorFeedbackSummaryViewModel>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<RealtorFeedbackSummaryDto>(
                ApiClientSupport.JsonOptions,
                cancellationToken);

            if (payload is null)
            {
                return ApiClientResult<RealtorFeedbackSummaryViewModel>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API returned an empty feedback summary payload."));
            }

            return ApiClientResult<RealtorFeedbackSummaryViewModel>.Success(new RealtorFeedbackSummaryViewModel
            {
                RealtorId = payload.RealtorId,
                TotalFeedbackCount = payload.TotalFeedbackCount,
                ServiceFeedbackCount = payload.ServiceFeedbackCount,
                PropertyFeedbackCount = payload.PropertyFeedbackCount,
                PurchaseFeedbackCount = payload.PurchaseFeedbackCount,
                SaleFeedbackCount = payload.SaleFeedbackCount,
                AverageServiceScore = payload.AverageServiceScore,
                AverageCommunicationScore = payload.AverageCommunicationScore,
                AverageResponsivenessScore = payload.AverageResponsivenessScore,
                AverageExpertiseScore = payload.AverageExpertiseScore,
                AverageTitleAccuracyScore = payload.AverageTitleAccuracyScore,
                AverageDescriptionAccuracyScore = payload.AverageDescriptionAccuracyScore,
                AveragePhotosAccuracyScore = payload.AveragePhotosAccuracyScore,
                AverageCriteriaAccuracyScore = payload.AverageCriteriaAccuracyScore,
                RecentItems = (payload.RecentItems ?? [])
                    .Select(x => new RealtorFeedbackSummaryItemViewModel
                    {
                        FeedbackId = x.FeedbackId,
                        DealId = x.DealId,
                        FormType = x.FormType,
                        IsServiceFeedback = x.IsServiceFeedback,
                        IsPropertyFeedback = x.IsPropertyFeedback,
                        ServiceScore = x.ServiceScore,
                        CommunicationScore = x.CommunicationScore,
                        ResponsivenessScore = x.ResponsivenessScore,
                        ExpertiseScore = x.ExpertiseScore,
                        TitleAccuracyScore = x.TitleAccuracyScore,
                        DescriptionAccuracyScore = x.DescriptionAccuracyScore,
                        PhotosAccuracyScore = x.PhotosAccuracyScore,
                        CriteriaAccuracyScore = x.CriteriaAccuracyScore,
                        Comment = x.Comment,
                        CreatedDate = x.CreatedDate
                    })
                    .ToList()
            });
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<RealtorFeedbackSummaryViewModel>.Failure(
                ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<RealtorFeedbackSummaryViewModel>.Failure(
                ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public async Task<ApiClientResult<IReadOnlyList<RealtorScoreSnapshotViewModel>>> GetScoreHistoryAsync(
        Guid realtorId,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var safeLimit = Math.Clamp(limit, 1, 200);
            var response = await _httpClient.GetAsync(
                $"api/realtor-efficiency/realtors/{realtorId}/scores/history?limit={safeLimit}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<IReadOnlyList<RealtorScoreSnapshotViewModel>>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<List<RealtorScoreSnapshotDto>>(
                ApiClientSupport.JsonOptions,
                cancellationToken);

            if (payload is null)
            {
                return ApiClientResult<IReadOnlyList<RealtorScoreSnapshotViewModel>>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API returned an empty realtor score history payload."));
            }

            var mapped = payload
                .Select(MapSnapshot)
                .OrderByDescending(x => x.CreatedDate)
                .ToList();

            return ApiClientResult<IReadOnlyList<RealtorScoreSnapshotViewModel>>.Success(mapped);
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<IReadOnlyList<RealtorScoreSnapshotViewModel>>.Failure(
                ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<IReadOnlyList<RealtorScoreSnapshotViewModel>>.Failure(
                ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public async Task<ApiClientResult<RealtorFeedbackSummaryViewModel>> GetFeedbackSummaryAsync(
        Guid realtorId,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var safeLimit = Math.Clamp(limit, 1, 100);
            var response = await _httpClient.GetAsync(
                $"api/realtor-efficiency/realtors/{realtorId}/feedback-summary?limit={safeLimit}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<RealtorFeedbackSummaryViewModel>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<RealtorFeedbackSummaryDto>(
                ApiClientSupport.JsonOptions,
                cancellationToken);

            if (payload is null)
            {
                return ApiClientResult<RealtorFeedbackSummaryViewModel>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API returned an empty feedback summary payload."));
            }

            return ApiClientResult<RealtorFeedbackSummaryViewModel>.Success(new RealtorFeedbackSummaryViewModel
            {
                RealtorId = payload.RealtorId,
                TotalFeedbackCount = payload.TotalFeedbackCount,
                ServiceFeedbackCount = payload.ServiceFeedbackCount,
                PropertyFeedbackCount = payload.PropertyFeedbackCount,
                PurchaseFeedbackCount = payload.PurchaseFeedbackCount,
                SaleFeedbackCount = payload.SaleFeedbackCount,
                AverageServiceScore = payload.AverageServiceScore,
                AverageCommunicationScore = payload.AverageCommunicationScore,
                AverageResponsivenessScore = payload.AverageResponsivenessScore,
                AverageExpertiseScore = payload.AverageExpertiseScore,
                AverageTitleAccuracyScore = payload.AverageTitleAccuracyScore,
                AverageDescriptionAccuracyScore = payload.AverageDescriptionAccuracyScore,
                AveragePhotosAccuracyScore = payload.AveragePhotosAccuracyScore,
                AverageCriteriaAccuracyScore = payload.AverageCriteriaAccuracyScore,
                RecentItems = (payload.RecentItems ?? [])
                    .Select(x => new RealtorFeedbackSummaryItemViewModel
                    {
                        FeedbackId = x.FeedbackId,
                        DealId = x.DealId,
                        FormType = x.FormType,
                        IsServiceFeedback = x.IsServiceFeedback,
                        IsPropertyFeedback = x.IsPropertyFeedback,
                        ServiceScore = x.ServiceScore,
                        CommunicationScore = x.CommunicationScore,
                        ResponsivenessScore = x.ResponsivenessScore,
                        ExpertiseScore = x.ExpertiseScore,
                        TitleAccuracyScore = x.TitleAccuracyScore,
                        DescriptionAccuracyScore = x.DescriptionAccuracyScore,
                        PhotosAccuracyScore = x.PhotosAccuracyScore,
                        CriteriaAccuracyScore = x.CriteriaAccuracyScore,
                        Comment = x.Comment,
                        CreatedDate = x.CreatedDate
                    })
                    .ToList()
            });
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<RealtorFeedbackSummaryViewModel>.Failure(
                ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<RealtorFeedbackSummaryViewModel>.Failure(
                ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public async Task<ApiClientResult<RealtorEligibilitySettingsViewModel>> GetEligibilitySettingsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("api/realtor-efficiency/eligibility-settings", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<RealtorEligibilitySettingsViewModel>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<EligibilitySettingsDto>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<RealtorEligibilitySettingsViewModel>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API returned an empty eligibility settings payload."));
            }

            return ApiClientResult<RealtorEligibilitySettingsViewModel>.Success(MapEligibilitySettings(payload));
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<RealtorEligibilitySettingsViewModel>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<RealtorEligibilitySettingsViewModel>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public Task<ApiOperationResult> UpdateEligibilitySettingsAsync(
        RealtorEligibilitySettingsViewModel settings,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Put, "api/realtor-efficiency/eligibility-settings", new
        {
            restrictionsEnabled = settings.RestrictionsEnabled,
            minConfirmedHistoryDeals = settings.MinConfirmedHistoryDeals,
            criticalComplaintLookbackDays = settings.CriticalComplaintLookbackDays,
            scoreSnapshotMaxAgeHours = settings.ScoreSnapshotMaxAgeHours,
            blockOnCriticalComplaints = settings.BlockOnCriticalComplaints,
            priceTiers = settings.PriceTiers
                .OrderBy(x => x.SortOrder)
                .Select(x => new
                {
                    name = x.Name,
                    minPrice = x.MinPrice,
                    maxPrice = x.MaxPrice,
                    minClientTrustScore = x.MinClientTrustScore,
                    minAdminPerformanceScore = x.MinAdminPerformanceScore,
                    sortOrder = x.SortOrder
                })
        }, cancellationToken);
    }

    public async Task<ApiClientResult<RealtorCommissionSettingsViewModel>> GetCommissionSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("api/realtor-efficiency/commission-settings", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<RealtorCommissionSettingsViewModel>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<CommissionSettingsDto>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<RealtorCommissionSettingsViewModel>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API returned an empty commission settings payload."));
            }

            return ApiClientResult<RealtorCommissionSettingsViewModel>.Success(new RealtorCommissionSettingsViewModel
            {
                Items = (payload.Items ?? [])
                    .Select(x => new RealtorLevelCommissionViewModel
                    {
                        Level = x.Level,
                        Percent = x.Percent
                    })
                    .ToList()
            });
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<RealtorCommissionSettingsViewModel>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<RealtorCommissionSettingsViewModel>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public Task<ApiOperationResult> UpdateCommissionSettingsAsync(
        IReadOnlyList<RealtorLevelCommissionViewModel> items,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Put, "api/realtor-efficiency/commission-settings", new
        {
            items = items.Select(x => new
            {
                level = x.Level,
                percent = x.Percent
            })
        }, cancellationToken);
    }

    public async Task<ApiClientResult<RealtorLevelSettingsViewModel>> GetLevelSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("api/realtor-efficiency/level-settings", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<RealtorLevelSettingsViewModel>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<LevelSettingsDto>(ApiClientSupport.JsonOptions, cancellationToken);
            if (payload is null)
            {
                return ApiClientResult<RealtorLevelSettingsViewModel>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API returned an empty realtor level settings payload."));
            }

            return ApiClientResult<RealtorLevelSettingsViewModel>.Success(new RealtorLevelSettingsViewModel
            {
                DemotionBuffer = payload.DemotionBuffer,
                Rules = (payload.Rules ?? [])
                    .OrderBy(x => x.SortOrder)
                    .Select(x => new RealtorLevelRuleViewModel
                    {
                        Level = x.Level,
                        MinCompletedDeals = x.MinCompletedDeals,
                        MinClientTrustScore = x.MinClientTrustScore,
                        MinAdminPerformanceScore = x.MinAdminPerformanceScore,
                        SortOrder = x.SortOrder
                    })
                    .ToList()
            });
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<RealtorLevelSettingsViewModel>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<RealtorLevelSettingsViewModel>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public Task<ApiOperationResult> UpdateLevelSettingsAsync(
        RealtorLevelSettingsViewModel settings,
        CancellationToken cancellationToken = default)
    {
        return ApiClientSupport.SendAsync(_httpClient, HttpMethod.Put, "api/realtor-efficiency/level-settings", new
        {
            demotionBuffer = settings.DemotionBuffer,
            rules = settings.Rules
                .OrderBy(x => x.SortOrder)
                .Select(x => new
                {
                    level = x.Level,
                    minCompletedDeals = x.MinCompletedDeals,
                    minClientTrustScore = x.MinClientTrustScore,
                    minAdminPerformanceScore = x.MinAdminPerformanceScore,
                    sortOrder = x.SortOrder
                })
        }, cancellationToken);
    }

    private static RealtorEligibilitySettingsViewModel MapEligibilitySettings(EligibilitySettingsDto payload)
    {
        return new RealtorEligibilitySettingsViewModel
        {
            RestrictionsEnabled = payload.RestrictionsEnabled,
            MinConfirmedHistoryDeals = payload.MinConfirmedHistoryDeals,
            CriticalComplaintLookbackDays = payload.CriticalComplaintLookbackDays,
            ScoreSnapshotMaxAgeHours = payload.ScoreSnapshotMaxAgeHours,
            BlockOnCriticalComplaints = payload.BlockOnCriticalComplaints,
            PriceTiers = (payload.PriceTiers ?? [])
                .OrderBy(x => x.SortOrder)
                .Select(x => new RealtorEligibilityTierViewModel
                {
                    Name = x.Name,
                    MinPrice = x.MinPrice,
                    MaxPrice = x.MaxPrice,
                    MinClientTrustScore = x.MinClientTrustScore,
                    MinAdminPerformanceScore = x.MinAdminPerformanceScore,
                    SortOrder = x.SortOrder
                })
                .ToList()
        };
    }

    private static RealtorScoreSnapshotViewModel MapSnapshot(RealtorScoreSnapshotDto payload)
    {
        return new RealtorScoreSnapshotViewModel
        {
            SnapshotId = payload.SnapshotId,
            RealtorId = payload.RealtorId,
            ClientTrustScore = payload.ClientTrustScore,
            AdminPerformanceScore = payload.AdminPerformanceScore,
            ClientTrustBreakdown = new ClientTrustBreakdownViewModel
            {
                ClientServiceScoreComponent = payload.ClientTrustBreakdown.ClientServiceScoreComponent,
                ComplaintPenaltyComponent = payload.ClientTrustBreakdown.ComplaintPenaltyComponent
            },
            AdminPerformanceBreakdown = new AdminPerformanceBreakdownViewModel
            {
                PropertyDataQualityComponent = payload.AdminPerformanceBreakdown.PropertyDataQualityComponent,
                WorkflowDisciplineComponent = payload.AdminPerformanceBreakdown.WorkflowDisciplineComponent,
                BusinessResultComponent = payload.AdminPerformanceBreakdown.BusinessResultComponent,
                ReputationRiskComponent = payload.AdminPerformanceBreakdown.ReputationRiskComponent
            },
            CalculationVersion = payload.CalculationVersion,
            CreatedDate = payload.CreatedDate
        };
    }

    private sealed class RealtorScoreSnapshotDto
    {
        public Guid SnapshotId { get; set; }

        public Guid RealtorId { get; set; }

        public double ClientTrustScore { get; set; }

        public double AdminPerformanceScore { get; set; }

        public ClientTrustBreakdownDto ClientTrustBreakdown { get; set; } = new();

        public AdminPerformanceBreakdownDto AdminPerformanceBreakdown { get; set; } = new();

        public string? CalculationVersion { get; set; }

        public DateTime CreatedDate { get; set; }
    }

    private sealed class ClientTrustBreakdownDto
    {
        public double ClientServiceScoreComponent { get; set; }

        public double ComplaintPenaltyComponent { get; set; }
    }

    private sealed class AdminPerformanceBreakdownDto
    {
        public double PropertyDataQualityComponent { get; set; }

        public double WorkflowDisciplineComponent { get; set; }

        public double BusinessResultComponent { get; set; }

        public double ReputationRiskComponent { get; set; }
    }

    private sealed class DealFeedbackStateDto
    {
        public Guid DealId { get; set; }
        public string FormType { get; set; } = "Undefined";
        public bool IsCompleted { get; set; }
        public bool IsSubmitted { get; set; }
        public bool CanSubmit { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public DateTime? DeadlineUtc { get; set; }
        public int? DaysRemaining { get; set; }
        public string? BlockReasonCode { get; set; }
        public string? BlockReason { get; set; }
    }

    private sealed class RealtorFeedbackSummaryDto
    {
        public Guid RealtorId { get; set; }
        public int TotalFeedbackCount { get; set; }
        public int ServiceFeedbackCount { get; set; }
        public int PropertyFeedbackCount { get; set; }
        public int PurchaseFeedbackCount { get; set; }
        public int SaleFeedbackCount { get; set; }
        public double? AverageServiceScore { get; set; }
        public double? AverageCommunicationScore { get; set; }
        public double? AverageResponsivenessScore { get; set; }
        public double? AverageExpertiseScore { get; set; }
        public double? AverageTitleAccuracyScore { get; set; }
        public double? AverageDescriptionAccuracyScore { get; set; }
        public double? AveragePhotosAccuracyScore { get; set; }
        public double? AverageCriteriaAccuracyScore { get; set; }
        public List<RealtorFeedbackSummaryItemDto>? RecentItems { get; set; }
    }

    private sealed class RealtorFeedbackSummaryItemDto
    {
        public Guid FeedbackId { get; set; }
        public Guid DealId { get; set; }
        public string FormType { get; set; } = "Undefined";
        public bool IsServiceFeedback { get; set; }
        public bool IsPropertyFeedback { get; set; }
        public int ServiceScore { get; set; }
        public int? CommunicationScore { get; set; }
        public int? ResponsivenessScore { get; set; }
        public int? ExpertiseScore { get; set; }
        public int? TitleAccuracyScore { get; set; }
        public int? DescriptionAccuracyScore { get; set; }
        public int? PhotosAccuracyScore { get; set; }
        public int? CriteriaAccuracyScore { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    private sealed class EligibilitySettingsDto
    {
        public bool RestrictionsEnabled { get; set; }
        public int MinConfirmedHistoryDeals { get; set; }
        public int CriticalComplaintLookbackDays { get; set; }
        public int ScoreSnapshotMaxAgeHours { get; set; }
        public bool BlockOnCriticalComplaints { get; set; }
        public List<EligibilityTierDto>? PriceTiers { get; set; }
    }

    private sealed class EligibilityTierDto
    {
        public string Name { get; set; } = string.Empty;
        public decimal MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public double MinClientTrustScore { get; set; }
        public double MinAdminPerformanceScore { get; set; }
        public int SortOrder { get; set; }
    }

    private sealed class CommissionSettingsDto
    {
        public List<CommissionLevelDto>? Items { get; set; }
    }

    private sealed class CommissionLevelDto
    {
        public string Level { get; set; } = "Undefined";
        public decimal Percent { get; set; }
    }

    private sealed class LevelSettingsDto
    {
        public double DemotionBuffer { get; set; }
        public List<LevelRuleDto>? Rules { get; set; }
    }

    private sealed class LevelRuleDto
    {
        public string Level { get; set; } = "Undefined";
        public int MinCompletedDeals { get; set; }
        public double MinClientTrustScore { get; set; }
        public double MinAdminPerformanceScore { get; set; }
        public int SortOrder { get; set; }
    }

    private sealed class MyRealtorIdDto
    {
        public Guid RealtorId { get; set; }
    }
}
