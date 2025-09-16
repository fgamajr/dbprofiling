using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DbConnect.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddUserApiSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Apenas criar a nova tabela UserApiSettings, pois as outras já existem
            migrationBuilder.CreateTable(
                name: "UserApiSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", nullable: false),
                    ApiKeyEncrypted = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastValidatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserApiSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserApiSettings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserApiSettings_UserId_Provider",
                table: "UserApiSettings",
                columns: new[] { "UserId", "Provider" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserApiSettings");
        }
    }
}
