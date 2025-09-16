using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DbConnect.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddDataQualityTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DataQualityAnalyses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProfileId = table.Column<int>(type: "INTEGER", nullable: false),
                    TableName = table.Column<string>(type: "TEXT", nullable: false),
                    Schema = table.Column<string>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataQualityAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataQualityAnalyses_Profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DataQualityAnalyses_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DataQualityResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AnalysisId = table.Column<int>(type: "INTEGER", nullable: false),
                    RuleId = table.Column<string>(type: "TEXT", nullable: false),
                    RuleName = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Dimension = table.Column<string>(type: "TEXT", nullable: false),
                    Column = table.Column<string>(type: "TEXT", nullable: false),
                    SqlCondition = table.Column<string>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", nullable: false),
                    ExpectedPassRate = table.Column<double>(type: "REAL", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    ActualPassRate = table.Column<double>(type: "REAL", nullable: false),
                    TotalRecords = table.Column<long>(type: "INTEGER", nullable: false),
                    ValidRecords = table.Column<long>(type: "INTEGER", nullable: false),
                    InvalidRecords = table.Column<long>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    ExecutedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataQualityResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataQualityResults_DataQualityAnalyses_AnalysisId",
                        column: x => x.AnalysisId,
                        principalTable: "DataQualityAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DataQualityAnalyses_ProfileId",
                table: "DataQualityAnalyses",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_DataQualityAnalyses_UserId",
                table: "DataQualityAnalyses",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DataQualityResults_AnalysisId",
                table: "DataQualityResults",
                column: "AnalysisId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataQualityResults");

            migrationBuilder.DropTable(
                name: "DataQualityAnalyses");
        }
    }
}
