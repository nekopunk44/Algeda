using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertyMatchNotificationLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PropertyMatchNotificationLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationType = table.Column<int>(type: "integer", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyMatchNotificationLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyMatchNotificationLogs_PropertyId_NotificationType",
                table: "PropertyMatchNotificationLogs",
                columns: new[] { "PropertyId", "NotificationType" });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyMatchNotificationLogs_RequirementId_NotificationType",
                table: "PropertyMatchNotificationLogs",
                columns: new[] { "RequirementId", "NotificationType" });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyMatchNotificationLogs_RequirementId_PropertyId_Noti~",
                table: "PropertyMatchNotificationLogs",
                columns: new[] { "RequirementId", "PropertyId", "NotificationType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PropertyMatchNotificationLogs");
        }
    }
}
