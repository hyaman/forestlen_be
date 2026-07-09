using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForestIQ.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRetentionDaysToJobConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RetentionDays",
                table: "JobConfigurations",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RetentionDays",
                table: "JobConfigurations");
        }
    }
}
