using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerClientIdToProperty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OwnerClientId",
                table: "Properties",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Properties_OwnerClientId",
                table: "Properties",
                column: "OwnerClientId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Properties_OwnerClientId",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "OwnerClientId",
                table: "Properties");
        }
    }
}
