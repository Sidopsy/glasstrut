using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Glasstrut.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAchievementGoalId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Achievements_ChallengeId",
                table: "Achievements");

            migrationBuilder.AddColumn<Guid>(
                name: "ChallengeGoalId",
                table: "Achievements",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Achievements_ChallengeGoalId",
                table: "Achievements",
                column: "ChallengeGoalId");

            migrationBuilder.CreateIndex(
                name: "IX_Achievements_ChallengeId_ChallengeGoalId",
                table: "Achievements",
                columns: new[] { "ChallengeId", "ChallengeGoalId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Achievements_ChallengeGoals_ChallengeGoalId",
                table: "Achievements",
                column: "ChallengeGoalId",
                principalTable: "ChallengeGoals",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Achievements_ChallengeGoals_ChallengeGoalId",
                table: "Achievements");

            migrationBuilder.DropIndex(
                name: "IX_Achievements_ChallengeGoalId",
                table: "Achievements");

            migrationBuilder.DropIndex(
                name: "IX_Achievements_ChallengeId_ChallengeGoalId",
                table: "Achievements");

            migrationBuilder.DropColumn(
                name: "ChallengeGoalId",
                table: "Achievements");

            migrationBuilder.CreateIndex(
                name: "IX_Achievements_ChallengeId",
                table: "Achievements",
                column: "ChallengeId");
        }
    }
}
