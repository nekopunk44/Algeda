using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRealtorCommissionPayouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLevelManuallyAssigned",
                table: "Realtors",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "AgencyNetCommissionAmount",
                table: "Deals",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "AgencyNetCommissionCurrency",
                table: "Deals",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<decimal>(
                name: "RealtorCommissionPercent",
                table: "Deals",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RealtorPayoutAmount",
                table: "Deals",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "RealtorPayoutCurrency",
                table: "Deals",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.Sql("""
                UPDATE "Deals"
                SET "AgencyNetCommissionAmount" = "CommissionAmount",
                    "AgencyNetCommissionCurrency" = "CommissionCurrency",
                    "RealtorPayoutCurrency" = "CommissionCurrency"
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsLevelManuallyAssigned",
                table: "Realtors");

            migrationBuilder.DropColumn(
                name: "AgencyNetCommissionAmount",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "AgencyNetCommissionCurrency",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "RealtorCommissionPercent",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "RealtorPayoutAmount",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "RealtorPayoutCurrency",
                table: "Deals");
        }
    }
}
