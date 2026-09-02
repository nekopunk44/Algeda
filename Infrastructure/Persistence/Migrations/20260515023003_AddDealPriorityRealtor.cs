using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDealPriorityRealtor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PriorityRealtorId",
                table: "Deals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PriorityUntilUtc",
                table: "Deals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Deals_PriorityRealtorId",
                table: "Deals",
                column: "PriorityRealtorId");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_PriorityUntilUtc",
                table: "Deals",
                column: "PriorityUntilUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Deals_PriorityRealtorId",
                table: "Deals");

            migrationBuilder.DropIndex(
                name: "IX_Deals_PriorityUntilUtc",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "PriorityRealtorId",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "PriorityUntilUtc",
                table: "Deals");
        }
    }
}
