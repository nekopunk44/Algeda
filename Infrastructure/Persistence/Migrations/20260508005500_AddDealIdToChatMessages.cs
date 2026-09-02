using System;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260508005500_AddDealIdToChatMessages")]
    public partial class AddDealIdToChatMessages : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DealId",
                table: "ChatMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_DealId_CreatedDate",
                table: "ChatMessages",
                columns: new[] { "DealId", "CreatedDate" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_DealId_CreatedDate",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "DealId",
                table: "ChatMessages");
        }
    }
}
