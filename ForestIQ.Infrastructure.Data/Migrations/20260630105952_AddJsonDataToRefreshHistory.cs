using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForestIQ.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJsonDataToRefreshHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "JsonData",
                table: "RefreshHistories",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JsonData",
                table: "RefreshHistories");
        }
    }
}
