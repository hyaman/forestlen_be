using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForestIQ.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJobConfigurations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JobName = table.Column<string>(type: "TEXT", nullable: false),
                    CronExpression = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobConfigurations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobConfigurations_JobName",
                table: "JobConfigurations",
                column: "JobName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobConfigurations");
        }
    }
}
