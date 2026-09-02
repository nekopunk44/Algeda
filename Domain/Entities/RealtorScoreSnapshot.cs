using Domain.Common;
using Domain.Primitives;

namespace Domain.Entities
{
    public class RealtorScoreSnapshot : BaseEntity
    {
        public Guid RealtorId { get; private set; }
        public double ClientTrustScore { get; private set; }
        public double AdminPerformanceScore { get; private set; }

        public double ClientServiceScoreComponent { get; private set; }
        public double PropertyAccuracyScoreComponent { get; private set; }
        public double ComplaintPenaltyComponent { get; private set; }

        public double PropertyDataQualityComponent { get; private set; }
        public double WorkflowDisciplineComponent { get; private set; }
        public double BusinessResultComponent { get; private set; }
        public double ReputationRiskComponent { get; private set; }

        public string? CalculationVersion { get; private set; }

        private RealtorScoreSnapshot()
        {
        }

        public RealtorScoreSnapshot(
            Guid realtorId,
            double clientTrustScore,
            double adminPerformanceScore,
            double clientServiceScoreComponent,
            double propertyAccuracyScoreComponent,
            double complaintPenaltyComponent,
            double propertyDataQualityComponent,
            double workflowDisciplineComponent,
            double businessResultComponent,
            double reputationRiskComponent,
            string? calculationVersion = null)
        {
            RealtorId = realtorId;
            ClientTrustScore = Round2(clientTrustScore);
            AdminPerformanceScore = Round2(adminPerformanceScore);
            ClientServiceScoreComponent = Round2(clientServiceScoreComponent);
            PropertyAccuracyScoreComponent = Round2(propertyAccuracyScoreComponent);
            ComplaintPenaltyComponent = Round2(complaintPenaltyComponent);
            PropertyDataQualityComponent = Round2(propertyDataQualityComponent);
            WorkflowDisciplineComponent = Round2(workflowDisciplineComponent);
            BusinessResultComponent = Round2(businessResultComponent);
            ReputationRiskComponent = Round2(reputationRiskComponent);
            CalculationVersion = NormalizeVersion(calculationVersion);

            Validate();
        }

        private void Validate()
        {
            if (RealtorId == Guid.Empty)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(RealtorId)));

            ValidateScore(ClientTrustScore, nameof(ClientTrustScore));
            ValidateScore(AdminPerformanceScore, nameof(AdminPerformanceScore));

            ValidateScore(ClientServiceScoreComponent, nameof(ClientServiceScoreComponent));
            ValidateScore(PropertyAccuracyScoreComponent, nameof(PropertyAccuracyScoreComponent));
            ValidateScore(ComplaintPenaltyComponent, nameof(ComplaintPenaltyComponent));
            ValidateScore(PropertyDataQualityComponent, nameof(PropertyDataQualityComponent));
            ValidateScore(WorkflowDisciplineComponent, nameof(WorkflowDisciplineComponent));
            ValidateScore(BusinessResultComponent, nameof(BusinessResultComponent));
            ValidateScore(ReputationRiskComponent, nameof(ReputationRiskComponent));
        }

        private static void ValidateScore(double value, string fieldName)
        {
            if (value < 0d || value > 5d)
                throw new DomainException(ValidationMessages.InvalidProperty(fieldName));
        }

        private static double Round2(double value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        private static string? NormalizeVersion(string? version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return null;

            var normalized = version.Trim();
            return normalized.Length <= 64 ? normalized : normalized[..64];
        }
    }
}
