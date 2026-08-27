using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OnlineJudge.Infrastructure.Persistence;

#nullable disable

namespace OnlineJudge.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OnlineJudgeDbContext))]
[Migration("20260828090000_AddChallengePartialScoreProgress")]
public class AddChallengePartialScoreProgress : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsCompleted",
            table: "ChallengeTaskCompletions",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "UpdatedAt",
            table: "ChallengeTaskCompletions",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: DateTimeOffset.UnixEpoch);

        migrationBuilder.Sql("""
            UPDATE "ChallengeTaskCompletions"
            SET "UpdatedAt" = "CompletedAt";
            """);

        migrationBuilder.Sql("""
            UPDATE "ChallengeTaskCompletions" AS completion
            SET "IsCompleted" = CASE
                WHEN EXISTS (
                    SELECT 1
                    FROM "ChallengeTasks" AS task
                    WHERE task."Id" = completion."ChallengeTaskId"
                      AND task."TaskType" = 1
                ) THEN TRUE
                WHEN EXISTS (
                    SELECT 1
                    FROM "ChallengeTaskFileSubmissions" AS file_submission
                    WHERE file_submission."ChallengeTaskId" = completion."ChallengeTaskId"
                      AND file_submission."UserId" = completion."UserId"
                      AND file_submission."ReviewedAt" IS NOT NULL
                ) THEN TRUE
                ELSE FALSE
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "IsCompleted", table: "ChallengeTaskCompletions");
        migrationBuilder.DropColumn(name: "UpdatedAt", table: "ChallengeTaskCompletions");
    }
}
