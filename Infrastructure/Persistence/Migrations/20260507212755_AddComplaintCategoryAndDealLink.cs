using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddComplaintCategoryAndDealLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CommunicationScore",
                table: "RealtorFeedbacks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExpertiseScore",
                table: "RealtorFeedbacks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FormType",
                table: "RealtorFeedbacks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ResponsivenessScore",
                table: "RealtorFeedbacks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TitleAlignment",
                table: "RealtorFeedbacks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<Guid>(
                name: "TargetRealtorId",
                table: "Complaints",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "Complaints",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "DealId",
                table: "Complaints",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_Category",
                table: "Complaints",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_DealId",
                table: "Complaints",
                column: "DealId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Complaints_Category",
                table: "Complaints");

            migrationBuilder.DropIndex(
                name: "IX_Complaints_DealId",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "CommunicationScore",
                table: "RealtorFeedbacks");

            migrationBuilder.DropColumn(
                name: "ExpertiseScore",
                table: "RealtorFeedbacks");

            migrationBuilder.DropColumn(
                name: "FormType",
                table: "RealtorFeedbacks");

            migrationBuilder.DropColumn(
                name: "ResponsivenessScore",
                table: "RealtorFeedbacks");

            migrationBuilder.DropColumn(
                name: "TitleAlignment",
                table: "RealtorFeedbacks");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "DealId",
                table: "Complaints");

            migrationBuilder.AlterColumn<Guid>(
                name: "TargetRealtorId",
                table: "Complaints",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
