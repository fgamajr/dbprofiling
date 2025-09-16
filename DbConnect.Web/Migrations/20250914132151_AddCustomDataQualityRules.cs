using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DbConnect.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomDataQualityRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomDataQualityRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProfileId = table.Column<int>(type: "INTEGER", nullable: false),
                    Schema = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TableName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    RuleId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Dimension = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Column = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SqlCondition = table.Column<string>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ExpectedPassRate = table.Column<double>(type: "REAL", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomDataQualityRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomDataQualityRules_Profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomDataQualityRules_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomDataQualityRules_ProfileId",
                table: "CustomDataQualityRules",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomDataQualityRules_UserId_ProfileId_Schema_TableName_RuleId",
                table: "CustomDataQualityRules",
                columns: new[] { "UserId", "ProfileId", "Schema", "TableName", "RuleId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomDataQualityRules");
        }
    }
}
