using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Glasstrut.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddIsPerEntryAndStreakGoal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPerEntry",
                table: "ChallengeGoals",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPerEntry",
                table: "ChallengeGoals");
        }
    }
}
