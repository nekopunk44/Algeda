using Application.Options;

namespace Application.Services
{
    public sealed class RealtorEfficiencyFormulaCalculator
    {
        private const double ScoreMin = 0d;
        private const double ScoreMax = 5d;
        private const double WeightTolerance = 0.000001d;
        private readonly RealtorEfficiencyOptions _options;

        public RealtorEfficiencyFormulaCalculator(RealtorEfficiencyOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            ValidateOptions(_options);
        }

        public RealtorScoreCalculationResult Calculate(RealtorScoreCalculationInput input)
        {
            ArgumentNullException.ThrowIfNull(input);

            var hasCtsEvidence = input.ServiceFeedbackCount > 0 || input.ConfirmedComplaintCount > 0;
            var hasApsEvidence = input.ManagedPropertyCount > 0
                                 || input.ActivityEventCount > 0
                                 || input.TotalDealsInWindow > 0
                                 || input.ConfirmedComplaintCount > 0;

            var serviceScore = input.ServiceFeedbackCount > 0
                ? Clamp(input.AverageServiceRating)
                : Clamp(_options.ClientTrustScore.DefaultServiceScore);

            var complaintPenaltyScore = BuildPenaltyScore(
                input.ConfirmedComplaintCount,
                input.PartiallyConfirmedComplaintCount,
                input.CriticalComplaintCount,
                _options.ClientTrustScore.Penalties,
                hasCtsEvidence,
                _options.ClientTrustScore.DefaultComplaintScore);

            var clientTrustScore = hasCtsEvidence
                ? (_options.ClientTrustScore.ServiceWeight * serviceScore)
                  + (_options.ClientTrustScore.ComplaintPenaltyWeight * complaintPenaltyScore)
                : 0d;

            var propertyDataQualityScore = input.ManagedPropertyCount > 0
                ? Clamp(input.AveragePropertyDataQualityScore)
                : Clamp(_options.AdminPerformanceScore.DefaultPropertyDataQualityScore);

            var workflowDisciplineScore = BuildWorkflowDisciplineScore(input);
            var businessResultScore = BuildBusinessResultScore(input);
            var reputationRiskScore = BuildPenaltyScore(
                input.ConfirmedComplaintCount,
                input.PartiallyConfirmedComplaintCount,
                input.CriticalComplaintCount,
                _options.AdminPerformanceScore.ReputationPenalties,
                hasApsEvidence,
                _options.AdminPerformanceScore.DefaultReputationRiskScore);

            var adminPerformanceScore = hasApsEvidence
                ? (_options.AdminPerformanceScore.PropertyDataQualityWeight * propertyDataQualityScore)
                  + (_options.AdminPerformanceScore.WorkflowDisciplineWeight * workflowDisciplineScore)
                  + (_options.AdminPerformanceScore.BusinessResultWeight * businessResultScore)
                  + (_options.AdminPerformanceScore.ReputationRiskWeight * reputationRiskScore)
                : 0d;

            return new RealtorScoreCalculationResult(
                ClientTrustScore: Round2(Clamp(clientTrustScore)),
                AdminPerformanceScore: Round2(Clamp(adminPerformanceScore)),
                ClientServiceScoreComponent: Round2(serviceScore),
                PropertyAccuracyScoreComponent: 0d,
                ComplaintPenaltyComponent: Round2(complaintPenaltyScore),
                PropertyDataQualityComponent: Round2(propertyDataQualityScore),
                WorkflowDisciplineComponent: Round2(workflowDisciplineScore),
                BusinessResultComponent: Round2(businessResultScore),
                ReputationRiskComponent: Round2(reputationRiskScore));
        }

        private double BuildWorkflowDisciplineScore(RealtorScoreCalculationInput input)
        {
            if (input.ActivityEventCount == 0 && input.TotalDealsInWindow == 0)
                return Clamp(_options.AdminPerformanceScore.DefaultWorkflowDisciplineScore);

            var activityScore = NormalizeToScore(
                input.TotalActivityPoints,
                _options.AdminPerformanceScore.WorkflowDiscipline.TargetActivityPoints);

            var cancellationRateScore = ScoreMax;
            if (input.TotalDealsInWindow > 0)
            {
                var cancellationRate = (double)input.CancelledDealsInWindow / input.TotalDealsInWindow;
                cancellationRateScore = Clamp((1d - cancellationRate) * ScoreMax);
            }

            return Clamp(
                (_options.AdminPerformanceScore.WorkflowDiscipline.ActivityWeight * activityScore)
                + (_options.AdminPerformanceScore.WorkflowDiscipline.CancellationRateWeight * cancellationRateScore));
        }

        private double BuildBusinessResultScore(RealtorScoreCalculationInput input)
        {
            if (input.CompletedDealsInWindow == 0 && input.TotalCommissionInWindow <= 0m)
                return Clamp(_options.AdminPerformanceScore.DefaultBusinessResultScore);

            var completedDealsScore = NormalizeToScore(
                input.CompletedDealsInWindow,
                _options.AdminPerformanceScore.BusinessResult.TargetCompletedDeals);

            var commissionScore = NormalizeToScore(
                input.TotalCommissionInWindow,
                _options.AdminPerformanceScore.BusinessResult.TargetCommission);

            return Clamp(
                (_options.AdminPerformanceScore.BusinessResult.CompletedDealsWeight * completedDealsScore)
                + (_options.AdminPerformanceScore.BusinessResult.CommissionWeight * commissionScore));
        }

        private static double BuildPenaltyScore(
            int confirmedComplaintCount,
            int partiallyConfirmedComplaintCount,
            int criticalComplaintCount,
            PenaltyScoreOptions options,
            bool hasEvidence,
            double defaultScore)
        {
            if (!hasEvidence)
            {
                return Clamp(defaultScore);
            }

            if (confirmedComplaintCount <= 0
                && partiallyConfirmedComplaintCount <= 0
                && criticalComplaintCount <= 0)
            {
                return ScoreMax;
            }

            var penalty =
                (confirmedComplaintCount * options.PenaltyPerConfirmedComplaint)
                + (partiallyConfirmedComplaintCount * options.PenaltyPerPartiallyConfirmedComplaint)
                + (criticalComplaintCount * options.PenaltyPerCriticalComplaint);

            penalty = Math.Clamp(penalty, 0d, options.MaxPenalty);
            return Clamp(ScoreMax - penalty);
        }

        private static double NormalizeToScore(int value, int target)
        {
            if (target <= 0)
                return 0d;

            return Clamp(((double)value / target) * ScoreMax);
        }

        private static double NormalizeToScore(decimal value, decimal target)
        {
            if (target <= 0)
                return 0d;

            return Clamp((double)(value / target) * ScoreMax);
        }

        private static double Round2(double value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        private static double Clamp(double value)
        {
            return Math.Clamp(value, ScoreMin, ScoreMax);
        }

        private static void ValidateOptions(RealtorEfficiencyOptions options)
        {
            ValidateWeightSum(
                options.ClientTrustScore.ServiceWeight
                + options.ClientTrustScore.ComplaintPenaltyWeight,
                nameof(options.ClientTrustScore));

            ValidateWeightSum(
                options.AdminPerformanceScore.PropertyDataQualityWeight
                + options.AdminPerformanceScore.WorkflowDisciplineWeight
                + options.AdminPerformanceScore.BusinessResultWeight
                + options.AdminPerformanceScore.ReputationRiskWeight,
                nameof(options.AdminPerformanceScore));

            ValidateWeightSum(
                options.AdminPerformanceScore.PropertyDataQuality.OwnerContactWeight
                + options.AdminPerformanceScore.PropertyDataQuality.PhotosWeight
                + options.AdminPerformanceScore.PropertyDataQuality.CriteriaWeight
                + options.AdminPerformanceScore.PropertyDataQuality.ClientVerifiedAccuracyWeight,
                nameof(options.AdminPerformanceScore.PropertyDataQuality));

            ValidateWeightSum(
                options.AdminPerformanceScore.WorkflowDiscipline.ActivityWeight
                + options.AdminPerformanceScore.WorkflowDiscipline.CancellationRateWeight,
                nameof(options.AdminPerformanceScore.WorkflowDiscipline));

            ValidateWeightSum(
                options.AdminPerformanceScore.BusinessResult.CompletedDealsWeight
                + options.AdminPerformanceScore.BusinessResult.CommissionWeight,
                nameof(options.AdminPerformanceScore.BusinessResult));
        }

        private static void ValidateWeightSum(double value, string name)
        {
            if (Math.Abs(value - 1d) > WeightTolerance)
            {
                throw new InvalidOperationException($"Weight sum in section {name} must equal 1.");
            }
        }
    }

    public sealed record RealtorScoreCalculationInput(
        int ServiceFeedbackCount,
        double AverageServiceRating,
        int PropertyFeedbackCount,
        double AveragePropertyAccuracyScore,
        int ConfirmedComplaintCount,
        int PartiallyConfirmedComplaintCount,
        int CriticalComplaintCount,
        int ManagedPropertyCount,
        double AveragePropertyDataQualityScore,
        int ActivityEventCount,
        int TotalActivityPoints,
        int TotalDealsInWindow,
        int CompletedDealsInWindow,
        int CancelledDealsInWindow,
        decimal TotalCommissionInWindow);

    public sealed record RealtorScoreCalculationResult(
        double ClientTrustScore,
        double AdminPerformanceScore,
        double ClientServiceScoreComponent,
        double PropertyAccuracyScoreComponent,
        double ComplaintPenaltyComponent,
        double PropertyDataQualityComponent,
        double WorkflowDisciplineComponent,
        double BusinessResultComponent,
        double ReputationRiskComponent);
}
