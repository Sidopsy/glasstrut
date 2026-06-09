using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Glasstrut.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddChallengeQuantities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrencyName",
                table: "Challenges",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Cost",
                table: "ChallengePrizes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PointValue",
                table: "ChallengeGoals",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TargetValue",
                table: "ChallengeGoals",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "ChallengeGoals",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrencyName",
                table: "Challenges");

            migrationBuilder.DropColumn(
                name: "Cost",
                table: "ChallengePrizes");

            migrationBuilder.DropColumn(
                name: "PointValue",
                table: "ChallengeGoals");

            migrationBuilder.DropColumn(
                name: "TargetValue",
                table: "ChallengeGoals");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "ChallengeGoals");
        }
    }
}
