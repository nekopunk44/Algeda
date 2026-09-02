using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertyOriginalPriceAndCurrencyRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "OriginalPriceAmount",
                table: "Properties",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "OriginalPriceCurrency",
                table: "Properties",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.CreateTable(
                name: "CurrencyRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Symbol = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    RateToBase = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurrencyRates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CurrencyRates_Code",
                table: "CurrencyRates",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CurrencyRates_IsActive",
                table: "CurrencyRates",
                column: "IsActive");

            migrationBuilder.Sql("""
                UPDATE "Properties"
                SET "OriginalPriceAmount" = "Price",
                    "OriginalPriceCurrency" = 'USD'
                WHERE "OriginalPriceCurrency" IS NULL
                   OR "OriginalPriceCurrency" = ''
                   OR "OriginalPriceAmount" = 0;
                """);

            migrationBuilder.Sql("""
                INSERT INTO "CurrencyRates" ("Id", "Code", "Name", "Symbol", "RateToBase", "IsActive", "UpdatedAtUtc", "CreatedDate")
                SELECT '6a8d35af-b089-4eb9-918f-4c4a03f7ca42', 'USD', 'US Dollar', '$', 1.000000, TRUE, NOW(), NOW()
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "CurrencyRates"
                    WHERE "Code" = 'USD'
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CurrencyRates");

            migrationBuilder.DropColumn(
                name: "OriginalPriceAmount",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "OriginalPriceCurrency",
                table: "Properties");
        }
    }
}
