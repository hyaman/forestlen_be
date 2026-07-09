using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForestIQ.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateRefreshHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DCName",
                table: "RefreshHistories",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DiscoverID",
                table: "RefreshHistories",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DCName",
                table: "RefreshHistories");

            migrationBuilder.DropColumn(
                name: "DiscoverID",
                table: "RefreshHistories");
        }
    }
}
