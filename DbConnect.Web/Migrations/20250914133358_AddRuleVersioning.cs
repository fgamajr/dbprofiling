using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DbConnect.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddRuleVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomDataQualityRules_UserId_ProfileId_Schema_TableName_RuleId",
                table: "CustomDataQualityRules");

            migrationBuilder.AddColumn<string>(
                name: "ChangeReason",
                table: "CustomDataQualityRules",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLatestVersion",
                table: "CustomDataQualityRules",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "CustomDataQualityRules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_CustomDataQualityRule_UniqueLatestVersion",
                table: "CustomDataQualityRules",
                columns: new[] { "UserId", "ProfileId", "Schema", "TableName", "RuleId", "IsLatestVersion" },
                unique: true,
                filter: "IsLatestVersion = 1");

            migrationBuilder.CreateIndex(
                name: "IX_CustomDataQualityRule_UniqueRule",
                table: "CustomDataQualityRules",
                columns: new[] { "UserId", "ProfileId", "Schema", "TableName", "RuleId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomDataQualityRule_UniqueLatestVersion",
                table: "CustomDataQualityRules");

            migrationBuilder.DropIndex(
                name: "IX_CustomDataQualityRule_UniqueRule",
                table: "CustomDataQualityRules");

            migrationBuilder.DropColumn(
                name: "ChangeReason",
                table: "CustomDataQualityRules");

            migrationBuilder.DropColumn(
                name: "IsLatestVersion",
                table: "CustomDataQualityRules");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "CustomDataQualityRules");

            migrationBuilder.CreateIndex(
                name: "IX_CustomDataQualityRules_UserId_ProfileId_Schema_TableName_RuleId",
                table: "CustomDataQualityRules",
                columns: new[] { "UserId", "ProfileId", "Schema", "TableName", "RuleId" },
                unique: true);
        }
    }
}
