using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MultiSych.Services.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentChatMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WebEditUrl",
                table: "CloudFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiCategory",
                table: "CachedEmails",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiSummary",
                table: "CachedEmails",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "CachedEmails",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "DocumentChatMessages",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    AccountId = table.Column<string>(type: "TEXT", nullable: false),
                    FileId = table.Column<string>(type: "TEXT", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    IsUser = table.Column<bool>(type: "INTEGER", nullable: false),
                    Time = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentChatMessages", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentChatMessages");

            migrationBuilder.DropColumn(
                name: "WebEditUrl",
                table: "CloudFiles");

            migrationBuilder.DropColumn(
                name: "AiCategory",
                table: "CachedEmails");

            migrationBuilder.DropColumn(
                name: "AiSummary",
                table: "CachedEmails");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "CachedEmails");
        }
    }
}
