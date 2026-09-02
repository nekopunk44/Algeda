using Application.DTOs.RealtorEfficiency;
using Application.Exceptions;
using Application.Interfaces;
using Application.Options;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Primitives;
using Microsoft.Extensions.Options;

namespace Application.Services
{
    public sealed class RealtorEfficiencyCalculationService : IRealtorEfficiencyCalculationService
    {
        private readonly IRealtorRepository _realtorRepository;
        private readonly IRealtorFeedbackRepository _feedbackRepository;
        private readonly IComplaintRepository _complaintRepository;
        private readonly IPropertyRepository _propertyRepository;
        private readonly IRealtorActivityLogRepository _activityLogRepository;
        private readonly IDealRepository _dealRepository;
        private readonly IRealtorScoreSnapshotRepository _snapshotRepository;
        private readonly IRealtorLevelCalculationService _levelCalculationService;
        private readonly IMapper _mapper;
        private readonly RealtorEfficiencyOptions _options;

        public RealtorEfficiencyCalculationService(
            IRealtorRepository realtorRepository,
            IRealtorFeedbackRepository feedbackRepository,
            IComplaintRepository complaintRepository,
            IPropertyRepository propertyRepository,
            IRealtorActivityLogRepository activityLogRepository,
            IDealRepository dealRepository,
            IRealtorScoreSnapshotRepository snapshotRepository,
            IRealtorLevelCalculationService levelCalculationService,
            IMapper mapper,
            IOptions<RealtorEfficiencyOptions> options)
        {
            _realtorRepository = realtorRepository;
            _feedbackRepository = feedbackRepository;
            _complaintRepository = complaintRepository;
            _propertyRepository = propertyRepository;
            _activityLogRepository = activityLogRepository;
            _dealRepository = dealRepository;
            _snapshotRepository = snapshotRepository;
            _levelCalculationService = levelCalculationService;
            _mapper = mapper;
            _options = options.Value;
        }

        public async Task<RealtorScoreBreakdownResponse> RecalculateAndSave(
            Guid realtorId,
            int? days = null,
            string? calculationVersion = null)
        {
            var realtor = await _realtorRepository.GetById(realtorId);
            if (realtor is null)
                throw new NotFoundException("Риелтор не найден.");

            var calculator = new RealtorEfficiencyFormulaCalculator(_options);
            var calculationInput = await BuildCalculationInput(realtorId, days ?? _options.CalculationWindowDays);
            var calculation = calculator.Calculate(calculationInput);

            var snapshot = new RealtorScoreSnapshot(
                realtorId,
                calculation.ClientTrustScore,
                calculation.AdminPerformanceScore,
                calculation.ClientServiceScoreComponent,
                calculation.PropertyAccuracyScoreComponent,
                calculation.ComplaintPenaltyComponent,
                calculation.PropertyDataQualityComponent,
                calculation.WorkflowDisciplineComponent,
                calculation.BusinessResultComponent,
                calculation.ReputationRiskComponent,
                calculationVersion ?? "cts-aps-v2");

            await _snapshotRepository.Add(snapshot);
            await _levelCalculationService.Recalculate(realtorId);
            return _mapper.Map<RealtorScoreBreakdownResponse>(snapshot);
        }

        public async Task<RealtorScoreBreakdownResponse?> GetLatestBreakdown(Guid realtorId)
        {
            var snapshot = await _snapshotRepository.GetLatestByRealtor(realtorId);
            return snapshot is null
                ? null
                : _mapper.Map<RealtorScoreBreakdownResponse>(snapshot);
        }

        public async Task<List<RealtorScoreBreakdownResponse>> GetHistory(Guid realtorId, int limit = 20)
        {
            var snapshots = await _snapshotRepository.GetHistoryByRealtor(realtorId, limit);
            return _mapper.Map<List<RealtorScoreBreakdownResponse>>(snapshots);
        }

        private async Task<RealtorScoreCalculationInput> BuildCalculationInput(Guid realtorId, int days)
        {
            if (days <= 0)
                throw new ValidationException("Период расчета должен быть больше 0 дней.");

            var fromUtc = DateTime.UtcNow.AddDays(-days);
            var serviceFeedback = await _feedbackRepository.GetByServiceRealtor(realtorId, fromUtc);
            var propertyFeedback = await _feedbackRepository.GetByPropertyResponsibleRealtor(realtorId, fromUtc);
            var resolvedComplaints = await _complaintRepository.GetResolvedByRealtor(realtorId, fromUtc);
            var properties = await _propertyRepository.GetByResponsibleRealtor(realtorId, fromUtc);
            var activities = await _activityLogRepository.GetByRealtor(realtorId, fromUtc);
            var deals = await _dealRepository.GetByRealtor(realtorId);
            var completedDeals = await _dealRepository.GetCompletedByRealtor(realtorId, fromUtc);

            var purchaseFeedback = propertyFeedback
                .Where(x => x.FormType == FeedbackFormType.Purchase)
                .ToList();

            var averagePropertyAccuracyScore = purchaseFeedback.Count == 0
                ? 0d
                : purchaseFeedback
                    .Select(feedback =>
                    {
                        var values = new[]
                        {
                            feedback.TitleAccuracyScore,
                            feedback.CriteriaAccuracyScore,
                            feedback.DescriptionAccuracyScore,
                            feedback.PhotosAccuracyScore
                        }
                        .Where(x => x.HasValue)
                        .Select(x => (double)x!.Value)
                        .ToList();

                        return values.Count == 0 ? 0d : values.Average();
                    })
                    .DefaultIfEmpty(0d)
                    .Average();

            var averageServiceScore = serviceFeedback.Count == 0
                ? 0d
                : serviceFeedback
                    .Select(feedback =>
                    {
                        var values = new List<double> { feedback.ServiceScore };
                        if (feedback.CommunicationScore.HasValue)
                            values.Add(feedback.CommunicationScore.Value);
                        if (feedback.ResponsivenessScore.HasValue)
                            values.Add(feedback.ResponsivenessScore.Value);
                        if (feedback.ExpertiseScore.HasValue)
                            values.Add(feedback.ExpertiseScore.Value);

                        return values.Average();
                    })
                    .Average();

            var structuralPropertyQualityScore = properties.Count == 0
                ? 0d
                : properties
                    .Select(CalculateStructuredPropertyDataQualityScore)
                    .Average();

            var dataQualityOptions = _options.AdminPerformanceScore.PropertyDataQuality;
            var structuralWeight = dataQualityOptions.OwnerContactWeight
                                   + dataQualityOptions.PhotosWeight
                                   + dataQualityOptions.CriteriaWeight;
            var clientWeight = dataQualityOptions.ClientVerifiedAccuracyWeight;

            var averagePropertyDataQualityScore = 0d;
            if (properties.Count > 0 && purchaseFeedback.Count > 0)
            {
                averagePropertyDataQualityScore =
                    (structuralWeight * structuralPropertyQualityScore)
                    + (clientWeight * averagePropertyAccuracyScore);
            }
            else if (properties.Count > 0)
            {
                averagePropertyDataQualityScore = structuralPropertyQualityScore;
            }
            else if (purchaseFeedback.Count > 0)
            {
                averagePropertyDataQualityScore = averagePropertyAccuracyScore;
            }

            var confirmedComplaintCount = resolvedComplaints.Count(x => x.ModerationVerdict == ComplaintReviewVerdict.Confirmed);
            var partiallyConfirmedComplaintCount = resolvedComplaints.Count(x => x.ModerationVerdict == ComplaintReviewVerdict.PartiallyConfirmed);
            var criticalComplaintCount = resolvedComplaints
                .Where(x => x.ModerationVerdict is ComplaintReviewVerdict.Confirmed or ComplaintReviewVerdict.PartiallyConfirmed)
                .Count(IsCriticalComplaint);
            var totalDealsInWindow = deals.Count(x => x.CreatedDate >= fromUtc);
            var cancelledDealsInWindow = deals.Count(x => x.CreatedDate >= fromUtc && x.Status == DealStatus.Cancelled);

            return new RealtorScoreCalculationInput(
                ServiceFeedbackCount: serviceFeedback.Count,
                AverageServiceRating: averageServiceScore,
                PropertyFeedbackCount: purchaseFeedback.Count,
                AveragePropertyAccuracyScore: averagePropertyAccuracyScore,
                ConfirmedComplaintCount: confirmedComplaintCount,
                PartiallyConfirmedComplaintCount: partiallyConfirmedComplaintCount,
                CriticalComplaintCount: criticalComplaintCount,
                ManagedPropertyCount: properties.Count,
                AveragePropertyDataQualityScore: averagePropertyDataQualityScore,
                ActivityEventCount: activities.Count,
                TotalActivityPoints: activities.Sum(x => x.Points),
                TotalDealsInWindow: totalDealsInWindow,
                CompletedDealsInWindow: completedDeals.Count,
                CancelledDealsInWindow: cancelledDealsInWindow,
                TotalCommissionInWindow: completedDeals.Sum(x => x.CommissionAmount));
        }

        private double CalculateStructuredPropertyDataQualityScore(Property property)
        {
            var ownerFields = new[]
            {
                !string.IsNullOrWhiteSpace(property.OwnerFullName),
                !string.IsNullOrWhiteSpace(property.OwnerEmail),
                !string.IsNullOrWhiteSpace(property.OwnerPhoneNumber)
            };

            var ownerContactScore = (ownerFields.Count(x => x) / 3d) * 5d;

            var targetPhotoCount = Math.Max(1, _options.AdminPerformanceScore.PropertyDataQuality.TargetPhotoCount);
            var photosScore = Math.Clamp((double)property.PhotoPaths.Count / targetPhotoCount * 5d, 0d, 5d);

            var targetCriteriaCount = Math.Max(1, _options.AdminPerformanceScore.PropertyDataQuality.TargetCriteriaCount);
            var criteriaScore = Math.Clamp((double)property.CriterionValues.Count / targetCriteriaCount * 5d, 0d, 5d);

            var dataQualityOptions = _options.AdminPerformanceScore.PropertyDataQuality;
            var structuralWeight = dataQualityOptions.OwnerContactWeight
                                   + dataQualityOptions.PhotosWeight
                                   + dataQualityOptions.CriteriaWeight;

            if (structuralWeight <= 0)
            {
                return 0d;
            }

            var weighted = (dataQualityOptions.OwnerContactWeight * ownerContactScore)
                           + (dataQualityOptions.PhotosWeight * photosScore)
                           + (dataQualityOptions.CriteriaWeight * criteriaScore);

            return weighted / structuralWeight;
        }

        private bool IsCriticalComplaint(Complaint complaint)
        {
            var keywords = _options.ComplaintClassification.CriticalKeywords ?? [];
            if (keywords.Length == 0)
                return false;

            return keywords.Any(keyword =>
                ContainsIgnoreCase(complaint.Subject, keyword)
                || ContainsIgnoreCase(complaint.Description, keyword)
                || ContainsIgnoreCase(complaint.AdminResolution, keyword));
        }

        private static bool ContainsIgnoreCase(string? source, string needle)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(needle))
                return false;

            return source.Contains(needle, StringComparison.OrdinalIgnoreCase);
        }
    }
}
