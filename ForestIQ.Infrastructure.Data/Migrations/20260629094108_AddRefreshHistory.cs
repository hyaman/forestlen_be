using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForestIQ.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RefreshHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SectionName = table.Column<string>(type: "TEXT", nullable: false),
                    RefreshTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TriggeredBy = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshHistories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshHistories_RefreshTime",
                table: "RefreshHistories",
                column: "RefreshTime",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshHistories_SectionName",
                table: "RefreshHistories",
                column: "SectionName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RefreshHistories");
        }
    }
}
