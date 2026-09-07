using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClientRequirementExtendedFilters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AddressQuery",
                table: "ClientRequirements",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int[]>(
                name: "DesiredTypes",
                table: "ClientRequirements",
                type: "integer[]",
                nullable: false,
                defaultValue: new int[0]);

            migrationBuilder.AddColumn<bool>(
                name: "IgnoreArea",
                table: "ClientRequirements",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "MaxArea",
                table: "ClientRequirements",
                type: "double precision",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "ClientRequirements"
                SET "DesiredTypes" = ARRAY["DesiredType"]::integer[]
                WHERE (array_length("DesiredTypes", 1) IS NULL)
                  AND "DesiredType" <> 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddressQuery",
                table: "ClientRequirements");

            migrationBuilder.DropColumn(
                name: "DesiredTypes",
                table: "ClientRequirements");

            migrationBuilder.DropColumn(
                name: "IgnoreArea",
                table: "ClientRequirements");

            migrationBuilder.DropColumn(
                name: "MaxArea",
                table: "ClientRequirements");
        }
    }
}
