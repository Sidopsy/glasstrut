using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Glasstrut.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddChallengeActivityGoalJoinTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create the join table first
            migrationBuilder.CreateTable(
                name: "ChallengeActivityGoals",
                columns: table => new
                {
                    ChallengeActivityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChallengeGoalId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChallengeActivityGoals", x => new { x.ChallengeActivityId, x.ChallengeGoalId });
                    table.ForeignKey(
                        name: "FK_ChallengeActivityGoals_ChallengeActivities_ChallengeActivityId",
                        column: x => x.ChallengeActivityId,
                        principalTable: "ChallengeActivities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChallengeActivityGoals_ChallengeGoals_ChallengeGoalId",
                        column: x => x.ChallengeGoalId,
                        principalTable: "ChallengeGoals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Drop the old constraints
            migrationBuilder.DropForeignKey(
                name: "FK_ChallengeActivities_ChallengeGoals_ChallengeGoalId",
                table: "ChallengeActivities");

            migrationBuilder.DropIndex(
                name: "IX_ChallengeActivities_ChallengeGoalId",
                table: "ChallengeActivities");

            // Migrate existing ChallengeGoalId values to the join table
            migrationBuilder.Sql(@"
                INSERT INTO ChallengeActivityGoals (ChallengeActivityId, ChallengeGoalId)
                SELECT Id, ChallengeGoalId
                FROM ChallengeActivities
                WHERE ChallengeGoalId IS NOT NULL
            ");

            // Drop the old column
            migrationBuilder.DropColumn(
                name: "ChallengeGoalId",
                table: "ChallengeActivities");

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeActivityGoals_ChallengeGoalId",
                table: "ChallengeActivityGoals",
                column: "ChallengeGoalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChallengeActivityGoals");

            migrationBuilder.AddColumn<Guid>(
                name: "ChallengeGoalId",
                table: "ChallengeActivities",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeActivities_ChallengeGoalId",
                table: "ChallengeActivities",
                column: "ChallengeGoalId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChallengeActivities_ChallengeGoals_ChallengeGoalId",
                table: "ChallengeActivities",
                column: "ChallengeGoalId",
                principalTable: "ChallengeGoals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
