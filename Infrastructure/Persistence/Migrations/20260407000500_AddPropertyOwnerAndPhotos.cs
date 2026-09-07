using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertyOwnerAndPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnerEmail",
                table: "Properties",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerFullName",
                table: "Properties",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerPhoneNumber",
                table: "Properties",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "PhotoPaths",
                table: "Properties",
                type: "text[]",
                nullable: false,
                defaultValue: new List<string>());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OwnerEmail",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "OwnerFullName",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "OwnerPhoneNumber",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "PhotoPaths",
                table: "Properties");
        }
    }
}
