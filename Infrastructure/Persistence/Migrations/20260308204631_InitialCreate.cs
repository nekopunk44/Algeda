using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiverId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClientRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DesiredType = table.Column<int>(type: "integer", nullable: false),
                    TargetLocation = table.Column<Point>(type: "geography (point)", nullable: false),
                    SearchRadiusMeters = table.Column<double>(type: "double precision", nullable: false),
                    MinPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MinArea = table.Column<double>(type: "double precision", nullable: false),
                    PriceWeight = table.Column<double>(type: "double precision", nullable: false),
                    AreaWeight = table.Column<double>(type: "double precision", nullable: false),
                    MinMatchPercentage = table.Column<double>(type: "double precision", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientRequirements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Complaints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetRealtorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AdminResolution = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Complaints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Deals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    RealtorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CommissionCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Properties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Area = table.Column<double>(type: "double precision", nullable: false),
                    RoomsCount = table.Column<int>(type: "integer", nullable: false),
                    Location = table.Column<Point>(type: "geography (point)", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Properties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RealtorActivityLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RealtorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RealtorActivityLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Realtors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CurrentKpiScore = table.Column<double>(type: "double precision", nullable: false),
                    AverageRating = table.Column<double>(type: "double precision", nullable: false),
                    DealsThisMonth = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Realtors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DealId = table.Column<Guid>(type: "uuid", nullable: false),
                    RealtorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_SenderId_ReceiverId_CreatedDate",
                table: "ChatMessages",
                columns: new[] { "SenderId", "ReceiverId", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientRequirements_ClientId",
                table: "ClientRequirements",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientRequirements_IsActive",
                table: "ClientRequirements",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ClientRequirements_TargetLocation",
                table: "ClientRequirements",
                column: "TargetLocation")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_PhoneNumber",
                table: "Clients",
                column: "PhoneNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_ClientId",
                table: "Complaints",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_Status",
                table: "Complaints",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_TargetRealtorId",
                table: "Complaints",
                column: "TargetRealtorId");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_ClientId",
                table: "Deals",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_CompletedAt",
                table: "Deals",
                column: "CompletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_PropertyId",
                table: "Deals",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_RealtorId",
                table: "Deals",
                column: "RealtorId");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_Status",
                table: "Deals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_Location",
                table: "Properties",
                column: "Location")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_Price",
                table: "Properties",
                column: "Price");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_Status",
                table: "Properties",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_Type",
                table: "Properties",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_RealtorActivityLogs_CreatedDate",
                table: "RealtorActivityLogs",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_RealtorActivityLogs_RealtorId",
                table: "RealtorActivityLogs",
                column: "RealtorId");

            migrationBuilder.CreateIndex(
                name: "IX_Realtors_CurrentKpiScore",
                table: "Realtors",
                column: "CurrentKpiScore");

            migrationBuilder.CreateIndex(
                name: "IX_Realtors_Level",
                table: "Realtors",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_Realtors_PhoneNumber",
                table: "Realtors",
                column: "PhoneNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ClientId",
                table: "Reviews",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_DealId",
                table: "Reviews",
                column: "DealId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_RealtorId",
                table: "Reviews",
                column: "RealtorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatMessages");

            migrationBuilder.DropTable(
                name: "ClientRequirements");

            migrationBuilder.DropTable(
                name: "Clients");

            migrationBuilder.DropTable(
                name: "Complaints");

            migrationBuilder.DropTable(
                name: "Deals");

            migrationBuilder.DropTable(
                name: "Properties");

            migrationBuilder.DropTable(
                name: "RealtorActivityLogs");

            migrationBuilder.DropTable(
                name: "Realtors");

            migrationBuilder.DropTable(
                name: "Reviews");
        }
    }
}
