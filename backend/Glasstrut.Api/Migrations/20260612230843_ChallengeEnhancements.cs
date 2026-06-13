using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Glasstrut.Api.Migrations
{
    /// <inheritdoc />
    public partial class ChallengeEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TimeAmount",
                table: "ProgressEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ChallengeGoalId",
                table: "ChallengePrizes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasQR",
                table: "ChallengePrizes",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                table: "ChallengeGoals",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ActivityType",
                table: "ChallengeActivities",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ChallengePrizes_ChallengeGoalId",
                table: "ChallengePrizes",
                column: "ChallengeGoalId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChallengePrizes_ChallengeGoals_ChallengeGoalId",
                table: "ChallengePrizes",
                column: "ChallengeGoalId",
                principalTable: "ChallengeGoals",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChallengePrizes_ChallengeGoals_ChallengeGoalId",
                table: "ChallengePrizes");

            migrationBuilder.DropIndex(
                name: "IX_ChallengePrizes_ChallengeGoalId",
                table: "ChallengePrizes");

            migrationBuilder.DropColumn(
                name: "TimeAmount",
                table: "ProgressEntries");

            migrationBuilder.DropColumn(
                name: "ChallengeGoalId",
                table: "ChallengePrizes");

            migrationBuilder.DropColumn(
                name: "HasQR",
                table: "ChallengePrizes");

            migrationBuilder.DropColumn(
                name: "IsHidden",
                table: "ChallengeGoals");

            migrationBuilder.DropColumn(
                name: "ActivityType",
                table: "ChallengeActivities");
        }
    }
}
