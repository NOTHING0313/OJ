using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineJudge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BindSubmissionsToJudgeRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProblemJudgeRevisionId",
                table: "Submissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_ProblemJudgeRevisionId",
                table: "Submissions",
                column: "ProblemJudgeRevisionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Submissions_ProblemJudgeRevisions_ProblemJudgeRevisionId",
                table: "Submissions",
                column: "ProblemJudgeRevisionId",
                principalTable: "ProblemJudgeRevisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "Submissions"
                        WHERE "Status" = 2) THEN
                        RAISE EXCEPTION 'Judging submissions must be drained or reconciled before migration.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM "Submissions" AS s
                        JOIN "Problems" AS p ON p."Id" = s."ProblemId"
                        LEFT JOIN "ProblemJudgeRevisions" AS r
                          ON r."Id" = p."CurrentJudgeRevisionId" AND r."ProblemId" = p."Id"
                        WHERE s."Status" = 1
                          AND r."Id" IS NULL) THEN
                        RAISE EXCEPTION 'Pending submissions require a current judge revision before migration.';
                    END IF;
                END $$;

                UPDATE "Submissions" AS s
                SET "ProblemJudgeRevisionId" = p."CurrentJudgeRevisionId"
                FROM "Problems" AS p
                WHERE p."Id" = s."ProblemId"
                  AND s."Status" = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Submissions_ProblemJudgeRevisions_ProblemJudgeRevisionId",
                table: "Submissions");

            migrationBuilder.DropIndex(
                name: "IX_Submissions_ProblemJudgeRevisionId",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ProblemJudgeRevisionId",
                table: "Submissions");
        }
    }
}
