using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Glasstrut.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMetricCategoryToGoals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MetricCategory",
                table: "ChallengeGoals",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MetricCategory",
                table: "ChallengeGoals");
        }
    }
}
