using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertyCriteria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PropertyCriterionDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ValueType = table.Column<int>(type: "integer", nullable: false),
                    IsHidden = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyCriterionDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PropertyCriterionOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyCriterionDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyCriterionOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyCriterionOptions_PropertyCriterionDefinitions_Prope~",
                        column: x => x.PropertyCriterionDefinitionId,
                        principalTable: "PropertyCriterionDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PropertyCriterionValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CriterionDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyCriterionValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyCriterionValues_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PropertyCriterionValues_PropertyCriterionDefinitions_Criter~",
                        column: x => x.CriterionDefinitionId,
                        principalTable: "PropertyCriterionDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyCriterionDefinitions_Category",
                table: "PropertyCriterionDefinitions",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyCriterionDefinitions_Code",
                table: "PropertyCriterionDefinitions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PropertyCriterionDefinitions_IsHidden",
                table: "PropertyCriterionDefinitions",
                column: "IsHidden");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyCriterionOptions_PropertyCriterionDefinitionId_Sort~",
                table: "PropertyCriterionOptions",
                columns: new[] { "PropertyCriterionDefinitionId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyCriterionOptions_PropertyCriterionDefinitionId_Value",
                table: "PropertyCriterionOptions",
                columns: new[] { "PropertyCriterionDefinitionId", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PropertyCriterionValues_CriterionDefinitionId",
                table: "PropertyCriterionValues",
                column: "CriterionDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyCriterionValues_PropertyId_CriterionDefinitionId",
                table: "PropertyCriterionValues",
                columns: new[] { "PropertyId", "CriterionDefinitionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PropertyCriterionOptions");

            migrationBuilder.DropTable(
                name: "PropertyCriterionValues");

            migrationBuilder.DropTable(
                name: "PropertyCriterionDefinitions");
        }
    }
}
