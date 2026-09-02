using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExtendDealWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AcceptedAtUtc",
                table: "Deals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientRequirementId",
                table: "Deals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAtUtc",
                table: "Deals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestMessage",
                table: "Deals",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "Deals",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql("UPDATE \"Deals\" SET \"Source\" = 1 WHERE \"Source\" = 0;");

            migrationBuilder.CreateTable(
                name: "DealNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DealId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorRealtorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DealNotes_Deals_DealId",
                        column: x => x.DealId,
                        principalTable: "Deals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Deals_ClientRequirementId",
                table: "Deals",
                column: "ClientRequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_Source",
                table: "Deals",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_Status_RealtorId",
                table: "Deals",
                columns: new[] { "Status", "RealtorId" });

            migrationBuilder.CreateIndex(
                name: "IX_DealNotes_AuthorRealtorId",
                table: "DealNotes",
                column: "AuthorRealtorId");

            migrationBuilder.CreateIndex(
                name: "IX_DealNotes_CreatedDate",
                table: "DealNotes",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_DealNotes_DealId",
                table: "DealNotes",
                column: "DealId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DealNotes");

            migrationBuilder.DropIndex(
                name: "IX_Deals_ClientRequirementId",
                table: "Deals");

            migrationBuilder.DropIndex(
                name: "IX_Deals_Source",
                table: "Deals");

            migrationBuilder.DropIndex(
                name: "IX_Deals_Status_RealtorId",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "AcceptedAtUtc",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "ClientRequirementId",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "RejectedAtUtc",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "RequestMessage",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Deals");
        }
    }
}
