using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Glasstrut.Api.Migrations
{
    /// <inheritdoc />
    public partial class FixFKAndAddIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChallengeActivities_Challenges_ChallengeId",
                table: "ChallengeActivities");

            migrationBuilder.DropIndex(
                name: "IX_PrizeClaims_ChallengePrizeId",
                table: "PrizeClaims");

            migrationBuilder.CreateIndex(
                name: "IX_PrizeClaims_ChallengePrizeId_UserId",
                table: "PrizeClaims",
                columns: new[] { "ChallengePrizeId", "UserId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ChallengeActivities_Challenges_ChallengeId",
                table: "ChallengeActivities",
                column: "ChallengeId",
                principalTable: "Challenges",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChallengeActivities_Challenges_ChallengeId",
                table: "ChallengeActivities");

            migrationBuilder.DropIndex(
                name: "IX_PrizeClaims_ChallengePrizeId_UserId",
                table: "PrizeClaims");

            migrationBuilder.CreateIndex(
                name: "IX_PrizeClaims_ChallengePrizeId",
                table: "PrizeClaims",
                column: "ChallengePrizeId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChallengeActivities_Challenges_ChallengeId",
                table: "ChallengeActivities",
                column: "ChallengeId",
                principalTable: "Challenges",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
