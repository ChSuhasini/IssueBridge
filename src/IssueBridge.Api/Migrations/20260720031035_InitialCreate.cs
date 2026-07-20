using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IssueBridge.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Issues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GitHubIssueId = table.Column<long>(type: "INTEGER", nullable: false),
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Body = table.Column<string>(type: "TEXT", nullable: true),
                    State = table.Column<string>(type: "TEXT", nullable: false),
                    Labels = table.Column<string>(type: "TEXT", nullable: true),
                    GitHubUrl = table.Column<string>(type: "TEXT", nullable: false),
                    GitHubCreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    GitHubUpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Issues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LocalTaskInfos",
                columns: table => new
                {
                    IssueId = table.Column<int>(type: "INTEGER", nullable: false),
                    AssignedTo = table.Column<string>(type: "TEXT", nullable: true),
                    LocalStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalTaskInfos", x => x.IssueId);
                    table.ForeignKey(
                        name: "FK_LocalTaskInfos_Issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "Issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Issues_GitHubIssueId",
                table: "Issues",
                column: "GitHubIssueId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LocalTaskInfos");

            migrationBuilder.DropTable(
                name: "Issues");
        }
    }
}
