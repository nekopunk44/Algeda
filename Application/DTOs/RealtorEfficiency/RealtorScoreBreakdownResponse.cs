namespace Application.DTOs.RealtorEfficiency
{
    public record RealtorScoreBreakdownResponse(
        Guid SnapshotId,
        Guid RealtorId,
        double ClientTrustScore,
        double AdminPerformanceScore,
        ClientTrustBreakdownResponse ClientTrustBreakdown,
        AdminPerformanceBreakdownResponse AdminPerformanceBreakdown,
        string? CalculationVersion,
        DateTime CreatedDate);

    public record ClientTrustBreakdownResponse(
        double ClientServiceScoreComponent,
        double PropertyAccuracyScoreComponent,
        double ComplaintPenaltyComponent);

    public record AdminPerformanceBreakdownResponse(
        double PropertyDataQualityComponent,
        double WorkflowDisciplineComponent,
        double BusinessResultComponent,
        double ReputationRiskComponent);
}
