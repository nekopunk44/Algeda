using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeRealtorScoresToFivePointScale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "RealtorScoreSnapshots"
                SET
                    "ClientTrustScore" = LEAST(GREATEST(CASE WHEN "ClientTrustScore" > 5 AND "ClientTrustScore" <= 100 THEN "ClientTrustScore" / 20 ELSE "ClientTrustScore" END, 0), 5),
                    "AdminPerformanceScore" = LEAST(GREATEST(CASE WHEN "AdminPerformanceScore" > 5 AND "AdminPerformanceScore" <= 100 THEN "AdminPerformanceScore" / 20 ELSE "AdminPerformanceScore" END, 0), 5),
                    "ClientServiceScoreComponent" = LEAST(GREATEST(CASE WHEN "ClientServiceScoreComponent" > 5 AND "ClientServiceScoreComponent" <= 100 THEN "ClientServiceScoreComponent" / 20 ELSE "ClientServiceScoreComponent" END, 0), 5),
                    "PropertyAccuracyScoreComponent" = LEAST(GREATEST(CASE WHEN "PropertyAccuracyScoreComponent" > 5 AND "PropertyAccuracyScoreComponent" <= 100 THEN "PropertyAccuracyScoreComponent" / 20 ELSE "PropertyAccuracyScoreComponent" END, 0), 5),
                    "ComplaintPenaltyComponent" = LEAST(GREATEST(CASE WHEN "ComplaintPenaltyComponent" > 5 AND "ComplaintPenaltyComponent" <= 100 THEN "ComplaintPenaltyComponent" / 20 ELSE "ComplaintPenaltyComponent" END, 0), 5),
                    "PropertyDataQualityComponent" = LEAST(GREATEST(CASE WHEN "PropertyDataQualityComponent" > 5 AND "PropertyDataQualityComponent" <= 100 THEN "PropertyDataQualityComponent" / 20 ELSE "PropertyDataQualityComponent" END, 0), 5),
                    "WorkflowDisciplineComponent" = LEAST(GREATEST(CASE WHEN "WorkflowDisciplineComponent" > 5 AND "WorkflowDisciplineComponent" <= 100 THEN "WorkflowDisciplineComponent" / 20 ELSE "WorkflowDisciplineComponent" END, 0), 5),
                    "BusinessResultComponent" = LEAST(GREATEST(CASE WHEN "BusinessResultComponent" > 5 AND "BusinessResultComponent" <= 100 THEN "BusinessResultComponent" / 20 ELSE "BusinessResultComponent" END, 0), 5),
                    "ReputationRiskComponent" = LEAST(GREATEST(CASE WHEN "ReputationRiskComponent" > 5 AND "ReputationRiskComponent" <= 100 THEN "ReputationRiskComponent" / 20 ELSE "ReputationRiskComponent" END, 0), 5);
                """);

            migrationBuilder.Sql("""
                UPDATE "Realtors"
                SET
                    "CurrentKpiScore" = LEAST(GREATEST(CASE WHEN "CurrentKpiScore" > 5 AND "CurrentKpiScore" <= 100 THEN "CurrentKpiScore" / 20 ELSE "CurrentKpiScore" END, 0), 5),
                    "AverageRating" = LEAST(GREATEST(CASE WHEN "AverageRating" > 5 AND "AverageRating" <= 100 THEN "AverageRating" / 20 ELSE "AverageRating" END, 0), 5);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
