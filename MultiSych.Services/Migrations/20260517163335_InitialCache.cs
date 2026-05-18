using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MultiSych.Services.Migrations
{
    /// <inheritdoc />
    public partial class InitialCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountCredentials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountId = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: true),
                    Provider = table.Column<string>(type: "TEXT", nullable: true),
                    AccessToken = table.Column<string>(type: "TEXT", nullable: true),
                    RefreshToken = table.Column<string>(type: "TEXT", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AdditionalPropertiesJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountCredentials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CachedEmails",
                columns: table => new
                {
                    MessageId = table.Column<string>(type: "TEXT", nullable: false),
                    AccountId = table.Column<string>(type: "TEXT", nullable: false),
                    Subject = table.Column<string>(type: "TEXT", nullable: true),
                    From = table.Column<string>(type: "TEXT", nullable: true),
                    To = table.Column<string>(type: "TEXT", nullable: true),
                    Cc = table.Column<string>(type: "TEXT", nullable: true),
                    Bcc = table.Column<string>(type: "TEXT", nullable: true),
                    Body = table.Column<string>(type: "TEXT", nullable: true),
                    IsHtml = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReceivedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CachedEmails", x => new { x.AccountId, x.MessageId });
                });

            migrationBuilder.CreateTable(
                name: "CachedEvents",
                columns: table => new
                {
                    EventId = table.Column<string>(type: "TEXT", nullable: false),
                    AccountId = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Location = table.Column<string>(type: "TEXT", nullable: true),
                    Attendees = table.Column<string>(type: "TEXT", nullable: true),
                    IsAllDay = table.Column<bool>(type: "INTEGER", nullable: false),
                    CalendarId = table.Column<string>(type: "TEXT", nullable: true),
                    Provider = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CachedEvents", x => new { x.AccountId, x.EventId });
                });

            migrationBuilder.CreateTable(
                name: "CachedFiles",
                columns: table => new
                {
                    FileId = table.Column<string>(type: "TEXT", nullable: false),
                    AccountId = table.Column<string>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: true),
                    FilePath = table.Column<string>(type: "TEXT", nullable: true),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    MimeType = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Owner = table.Column<string>(type: "TEXT", nullable: true),
                    IsDirectory = table.Column<bool>(type: "INTEGER", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CachedFiles", x => new { x.AccountId, x.FileId });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountCredentials");

            migrationBuilder.DropTable(
                name: "CachedEmails");

            migrationBuilder.DropTable(
                name: "CachedEvents");

            migrationBuilder.DropTable(
                name: "CachedFiles");
        }
    }
}
