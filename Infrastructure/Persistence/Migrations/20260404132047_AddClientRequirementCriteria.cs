using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClientRequirementCriteria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientRequirementCriteria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientRequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    CriterionDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true),
                    ValuesJson = table.Column<string>(type: "text", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientRequirementCriteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientRequirementCriteria_ClientRequirements_ClientRequirem~",
                        column: x => x.ClientRequirementId,
                        principalTable: "ClientRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientRequirementCriteria_PropertyCriterionDefinitions_Crit~",
                        column: x => x.CriterionDefinitionId,
                        principalTable: "PropertyCriterionDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientRequirementCriteria_ClientRequirementId_CriterionDefi~",
                table: "ClientRequirementCriteria",
                columns: new[] { "ClientRequirementId", "CriterionDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientRequirementCriteria_CriterionDefinitionId",
                table: "ClientRequirementCriteria",
                column: "CriterionDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientRequirementCriteria_Priority",
                table: "ClientRequirementCriteria",
                column: "Priority");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientRequirementCriteria");
        }
    }
}
