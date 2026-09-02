using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateRealtorEfficiencyV2AndComplaintModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CriteriaAccuracyScore",
                table: "RealtorFeedbacks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DescriptionAccuracyScore",
                table: "RealtorFeedbacks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PhotosAccuracyScore",
                table: "RealtorFeedbacks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TitleAccuracyScore",
                table: "RealtorFeedbacks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModerationVerdict",
                table: "Complaints",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "PropertyId",
                table: "Complaints",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_PropertyId",
                table: "Complaints",
                column: "PropertyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Complaints_PropertyId",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "CriteriaAccuracyScore",
                table: "RealtorFeedbacks");

            migrationBuilder.DropColumn(
                name: "DescriptionAccuracyScore",
                table: "RealtorFeedbacks");

            migrationBuilder.DropColumn(
                name: "PhotosAccuracyScore",
                table: "RealtorFeedbacks");

            migrationBuilder.DropColumn(
                name: "TitleAccuracyScore",
                table: "RealtorFeedbacks");

            migrationBuilder.DropColumn(
                name: "ModerationVerdict",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "PropertyId",
                table: "Complaints");
        }
    }
}
