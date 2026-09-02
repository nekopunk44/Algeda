using System;
using Domain.Primitives;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDealDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DealDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DealId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UploadedByDisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedByDisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DealDocuments_Deals_DealId",
                        column: x => x.DealId,
                        principalTable: "Deals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DealDocumentAccessLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DealId = table.Column<Guid>(type: "uuid", nullable: false),
                    DealDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DocumentFileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    Action = table.Column<DealDocumentAction>(type: "integer", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorDisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ActorRole = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealDocumentAccessLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DealDocumentAccessLogs_DealDocuments_DealDocumentId",
                        column: x => x.DealDocumentId,
                        principalTable: "DealDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DealDocumentAccessLogs_Deals_DealId",
                        column: x => x.DealId,
                        principalTable: "Deals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DealDocumentAccessLogs_Action",
                table: "DealDocumentAccessLogs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_DealDocumentAccessLogs_Action_CreatedDate",
                table: "DealDocumentAccessLogs",
                columns: new[] { "Action", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DealDocumentAccessLogs_ActorUserId",
                table: "DealDocumentAccessLogs",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DealDocumentAccessLogs_CreatedDate",
                table: "DealDocumentAccessLogs",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_DealDocumentAccessLogs_DealDocumentId",
                table: "DealDocumentAccessLogs",
                column: "DealDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DealDocumentAccessLogs_DealId",
                table: "DealDocumentAccessLogs",
                column: "DealId");

            migrationBuilder.CreateIndex(
                name: "IX_DealDocuments_DealId",
                table: "DealDocuments",
                column: "DealId");

            migrationBuilder.CreateIndex(
                name: "IX_DealDocuments_DealId_IsDeleted_CreatedDate",
                table: "DealDocuments",
                columns: new[] { "DealId", "IsDeleted", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DealDocuments_DeletedByUserId",
                table: "DealDocuments",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DealDocuments_UploadedByUserId",
                table: "DealDocuments",
                column: "UploadedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DealDocumentAccessLogs");

            migrationBuilder.DropTable(
                name: "DealDocuments");
        }
    }
}
