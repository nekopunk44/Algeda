using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRealtorEfficiencyFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ResponsibleRealtorId",
                table: "Properties",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RealtorFeedbacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DealId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceRealtorId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: true),
                    PropertyResponsibleRealtorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ServiceScore = table.Column<int>(type: "integer", nullable: false),
                    CriteriaAlignment = table.Column<int>(type: "integer", nullable: false),
                    DescriptionAlignment = table.Column<int>(type: "integer", nullable: false),
                    PhotosAlignment = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RealtorFeedbacks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RealtorScoreSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RealtorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientTrustScore = table.Column<double>(type: "double precision", nullable: false),
                    AdminPerformanceScore = table.Column<double>(type: "double precision", nullable: false),
                    ClientServiceScoreComponent = table.Column<double>(type: "double precision", nullable: false),
                    PropertyAccuracyScoreComponent = table.Column<double>(type: "double precision", nullable: false),
                    ComplaintPenaltyComponent = table.Column<double>(type: "double precision", nullable: false),
                    PropertyDataQualityComponent = table.Column<double>(type: "double precision", nullable: false),
                    WorkflowDisciplineComponent = table.Column<double>(type: "double precision", nullable: false),
                    BusinessResultComponent = table.Column<double>(type: "double precision", nullable: false),
                    ReputationRiskComponent = table.Column<double>(type: "double precision", nullable: false),
                    CalculationVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RealtorScoreSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Properties_ResponsibleRealtorId",
                table: "Properties",
                column: "ResponsibleRealtorId");

            migrationBuilder.CreateIndex(
                name: "IX_RealtorFeedbacks_ClientId",
                table: "RealtorFeedbacks",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_RealtorFeedbacks_DealId",
                table: "RealtorFeedbacks",
                column: "DealId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RealtorFeedbacks_PropertyResponsibleRealtorId",
                table: "RealtorFeedbacks",
                column: "PropertyResponsibleRealtorId");

            migrationBuilder.CreateIndex(
                name: "IX_RealtorFeedbacks_ServiceRealtorId",
                table: "RealtorFeedbacks",
                column: "ServiceRealtorId");

            migrationBuilder.CreateIndex(
                name: "IX_RealtorScoreSnapshots_RealtorId",
                table: "RealtorScoreSnapshots",
                column: "RealtorId");

            migrationBuilder.CreateIndex(
                name: "IX_RealtorScoreSnapshots_RealtorId_CreatedDate",
                table: "RealtorScoreSnapshots",
                columns: new[] { "RealtorId", "CreatedDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RealtorFeedbacks");

            migrationBuilder.DropTable(
                name: "RealtorScoreSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_Properties_ResponsibleRealtorId",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "ResponsibleRealtorId",
                table: "Properties");
        }
    }
}
