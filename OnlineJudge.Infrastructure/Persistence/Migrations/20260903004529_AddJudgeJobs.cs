using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineJudge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJudgeJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JudgeJobs",
                columns: table => new
                {
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    AvailableAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LeaseToken = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseOwner = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastAttemptStartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastFailureKind = table.Column<int>(type: "integer", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JudgeJobs", x => x.SubmissionId);
                    table.CheckConstraint("CK_JudgeJobs_AttemptCount", "\"AttemptCount\" >= 0");
                    table.CheckConstraint("CK_JudgeJobs_FailureKind", "\"LastFailureKind\" IS NULL OR \"LastFailureKind\" IN (1, 2)");
                    table.CheckConstraint("CK_JudgeJobs_LeaseState", "(\"Status\" = 1 AND \"LeaseToken\" IS NULL AND \"LeaseOwner\" IS NULL AND \"LeaseExpiresAt\" IS NULL AND \"FinishedAt\" IS NULL) OR (\"Status\" = 2 AND \"LeaseToken\" IS NOT NULL AND \"LeaseOwner\" IS NOT NULL AND length(\"LeaseOwner\") > 0 AND \"LeaseExpiresAt\" IS NOT NULL AND \"FinishedAt\" IS NULL) OR (\"Status\" IN (3, 4) AND \"LeaseToken\" IS NULL AND \"LeaseOwner\" IS NULL AND \"LeaseExpiresAt\" IS NULL AND \"FinishedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_JudgeJobs_Status", "\"Status\" BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "FK_JudgeJobs_Submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "Submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JudgeJobs_Status_AvailableAt_CreatedAt_SubmissionId",
                table: "JudgeJobs",
                columns: new[] { "Status", "AvailableAt", "CreatedAt", "SubmissionId" });

            migrationBuilder.CreateIndex(
                name: "IX_JudgeJobs_Status_LeaseExpiresAt_CreatedAt_SubmissionId",
                table: "JudgeJobs",
                columns: new[] { "Status", "LeaseExpiresAt", "CreatedAt", "SubmissionId" });

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "Submissions" AS submission
                        LEFT JOIN "ProblemJudgeRevisions" AS revision
                          ON revision."Id" = submission."ProblemJudgeRevisionId"
                         AND revision."ProblemId" = submission."ProblemId"
                        WHERE submission."Status" IN (1, 2)
                          AND revision."Id" IS NULL
                    ) THEN
                        RAISE EXCEPTION 'Cannot create judge jobs because a pending or judging submission has no valid judge revision.';
                    END IF;
                END $$;

                DELETE FROM "SubmissionCaseResults"
                WHERE "SubmissionId" IN (
                    SELECT "Id" FROM "Submissions" WHERE "Status" = 2
                );

                UPDATE "Submissions"
                SET "Status" = 1,
                    "TimeUsedMs" = NULL,
                    "MemoryUsedKb" = NULL,
                    "ErrorMessage" = NULL,
                    "FinishedAt" = NULL
                WHERE "Status" = 2;

                INSERT INTO "JudgeJobs"
                    ("SubmissionId", "Status", "AttemptCount", "AvailableAt", "CreatedAt", "UpdatedAt")
                SELECT submission."Id", 1, 0, NOW(), submission."CreatedAt", NOW()
                FROM "Submissions" AS submission
                WHERE submission."Status" = 1
                ON CONFLICT ("SubmissionId") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "JudgeJobs" WHERE "Status" IN (1, 2)) THEN
                        RAISE EXCEPTION 'Cannot remove judge jobs while pending or leased work exists.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropTable(
                name: "JudgeJobs");
        }
    }
}
