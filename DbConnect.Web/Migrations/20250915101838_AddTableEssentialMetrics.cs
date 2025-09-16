using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DbConnect.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddTableEssentialMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TableEssentialMetrics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProfileId = table.Column<int>(type: "INTEGER", nullable: false),
                    Schema = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TableName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CollectedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TotalRows = table.Column<long>(type: "INTEGER", nullable: false),
                    EstimatedSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalColumns = table.Column<int>(type: "INTEGER", nullable: false),
                    ColumnsWithNulls = table.Column<int>(type: "INTEGER", nullable: false),
                    OverallCompleteness = table.Column<double>(type: "REAL", nullable: false),
                    DuplicateRows = table.Column<long>(type: "INTEGER", nullable: false),
                    DuplicationRate = table.Column<double>(type: "REAL", nullable: false),
                    PrimaryKeyColumns = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TableEssentialMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TableEssentialMetrics_Profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TableEssentialMetrics_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ColumnEssentialMetrics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TableMetricsId = table.Column<int>(type: "INTEGER", nullable: false),
                    ColumnName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DataType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IsNullable = table.Column<bool>(type: "INTEGER", nullable: false),
                    TotalValues = table.Column<long>(type: "INTEGER", nullable: false),
                    NullValues = table.Column<long>(type: "INTEGER", nullable: false),
                    EmptyValues = table.Column<long>(type: "INTEGER", nullable: false),
                    CompletenessRate = table.Column<double>(type: "REAL", nullable: false),
                    UniqueValues = table.Column<long>(type: "INTEGER", nullable: false),
                    CardinalityRate = table.Column<double>(type: "REAL", nullable: false),
                    MinNumeric = table.Column<decimal>(type: "TEXT", nullable: true),
                    MaxNumeric = table.Column<decimal>(type: "TEXT", nullable: true),
                    AvgNumeric = table.Column<decimal>(type: "TEXT", nullable: true),
                    StdDevNumeric = table.Column<decimal>(type: "TEXT", nullable: true),
                    MinDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MaxDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MinLength = table.Column<int>(type: "INTEGER", nullable: true),
                    MaxLength = table.Column<int>(type: "INTEGER", nullable: true),
                    AvgLength = table.Column<double>(type: "REAL", nullable: true),
                    TopValuesJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ColumnEssentialMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ColumnEssentialMetrics_TableEssentialMetrics_TableMetricsId",
                        column: x => x.TableMetricsId,
                        principalTable: "TableEssentialMetrics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ColumnEssentialMetrics_TableMetricsId",
                table: "ColumnEssentialMetrics",
                column: "TableMetricsId");

            migrationBuilder.CreateIndex(
                name: "IX_TableEssentialMetrics_ProfileId",
                table: "TableEssentialMetrics",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_TableEssentialMetrics_UniqueTable",
                table: "TableEssentialMetrics",
                columns: new[] { "UserId", "ProfileId", "Schema", "TableName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ColumnEssentialMetrics");

            migrationBuilder.DropTable(
                name: "TableEssentialMetrics");
        }
    }
}
