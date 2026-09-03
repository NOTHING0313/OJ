using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineJudge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProblemJudgeRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProblemJudgeAssets_ProblemId_Language_NormalizedFileName",
                table: "ProblemJudgeAssets");

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentJudgeRevisionId",
                table: "Problems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "ProblemJudgeAssets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ProblemJudgeAssets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ProblemJudgeRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProblemId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    JudgeMode = table.Column<int>(type: "integer", nullable: false),
                    AllowedLanguagesMask = table.Column<int>(type: "integer", nullable: false),
                    FunctionSpecJson = table.Column<string>(type: "text", nullable: true),
                    TimeLimitMs = table.Column<int>(type: "integer", nullable: false),
                    MemoryLimitMb = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProblemJudgeRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProblemJudgeRevisions_Problems_ProblemId",
                        column: x => x.ProblemId,
                        principalTable: "Problems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProblemJudgeRevisionAssets",
                columns: table => new
                {
                    ProblemJudgeRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProblemJudgeAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProblemJudgeRevisionAssets", x => new { x.ProblemJudgeRevisionId, x.ProblemJudgeAssetId });
                    table.ForeignKey(
                        name: "FK_ProblemJudgeRevisionAssets_ProblemJudgeAssets_ProblemJudgeA~",
                        column: x => x.ProblemJudgeAssetId,
                        principalTable: "ProblemJudgeAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProblemJudgeRevisionAssets_ProblemJudgeRevisions_ProblemJud~",
                        column: x => x.ProblemJudgeRevisionId,
                        principalTable: "ProblemJudgeRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProblemJudgeRevisionTestCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProblemJudgeRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceTestCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Input = table.Column<string>(type: "text", nullable: false),
                    ExpectedOutput = table.Column<string>(type: "text", nullable: false),
                    ArgumentsJson = table.Column<string>(type: "text", nullable: true),
                    ExpectedJson = table.Column<string>(type: "text", nullable: true),
                    Visibility = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProblemJudgeRevisionTestCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProblemJudgeRevisionTestCases_ProblemJudgeRevisions_Problem~",
                        column: x => x.ProblemJudgeRevisionId,
                        principalTable: "ProblemJudgeRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProblemJudgeRevisionTestCases_TestCases_SourceTestCaseId",
                        column: x => x.SourceTestCaseId,
                        principalTable: "TestCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Problems_CurrentJudgeRevisionId",
                table: "Problems",
                column: "CurrentJudgeRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProblemJudgeAssets_ProblemId_Language_NormalizedFileName",
                table: "ProblemJudgeAssets",
                columns: new[] { "ProblemId", "Language", "NormalizedFileName" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_ProblemJudgeRevisionAssets_ProblemJudgeAssetId",
                table: "ProblemJudgeRevisionAssets",
                column: "ProblemJudgeAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_ProblemJudgeRevisionAssets_ProblemJudgeRevisionId_Order",
                table: "ProblemJudgeRevisionAssets",
                columns: new[] { "ProblemJudgeRevisionId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProblemJudgeRevisions_ProblemId_RevisionNumber",
                table: "ProblemJudgeRevisions",
                columns: new[] { "ProblemId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProblemJudgeRevisionTestCases_ProblemJudgeRevisionId_Order",
                table: "ProblemJudgeRevisionTestCases",
                columns: new[] { "ProblemJudgeRevisionId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProblemJudgeRevisionTestCases_ProblemJudgeRevisionId_Source~",
                table: "ProblemJudgeRevisionTestCases",
                columns: new[] { "ProblemJudgeRevisionId", "SourceTestCaseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProblemJudgeRevisionTestCases_SourceTestCaseId",
                table: "ProblemJudgeRevisionTestCases",
                column: "SourceTestCaseId");

            migrationBuilder.AddForeignKey(
                name: "FK_Problems_ProblemJudgeRevisions_CurrentJudgeRevisionId",
                table: "Problems",
                column: "CurrentJudgeRevisionId",
                principalTable: "ProblemJudgeRevisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "Problems" AS p
                        WHERE p."IsPublished" = TRUE
                          AND p."IsDeleted" = FALSE
                          AND NOT EXISTS (
                              SELECT 1
                              FROM "TestCases" AS t
                              WHERE t."ProblemId" = p."Id" AND t."IsDeleted" = FALSE)) THEN
                        RAISE EXCEPTION 'Published problems must contain at least one active test case before judge revisions can be created.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM "Problems" AS p
                        JOIN "TestCases" AS t ON t."ProblemId" = p."Id" AND t."IsDeleted" = FALSE
                        WHERE p."IsPublished" = TRUE
                          AND p."IsDeleted" = FALSE
                          AND ((p."JudgeMode" = 1 AND (
                                  NULLIF(BTRIM(t."ArgumentsJson"), '') IS NOT NULL
                                  OR NULLIF(BTRIM(t."ExpectedJson"), '') IS NOT NULL))
                               OR (p."JudgeMode" = 2 AND (
                                  NULLIF(BTRIM(t."Input"), '') IS NOT NULL
                                  OR NULLIF(BTRIM(t."ExpectedOutput"), '') IS NOT NULL
                                  OR NULLIF(BTRIM(t."ArgumentsJson"), '') IS NULL
                                  OR NULLIF(BTRIM(t."ExpectedJson"), '') IS NULL)))) THEN
                        RAISE EXCEPTION 'Published problems contain test cases incompatible with their judge mode.';
                    END IF;
                END $$;

                INSERT INTO "ProblemJudgeRevisions"
                    ("Id", "ProblemId", "RevisionNumber", "JudgeMode", "AllowedLanguagesMask", "FunctionSpecJson", "TimeLimitMs", "MemoryLimitMb", "CreatedAt")
                SELECT
                    md5(p."Id"::text || ':judge-revision:1')::uuid,
                    p."Id",
                    1,
                    p."JudgeMode",
                    p."AllowedLanguagesMask",
                    CASE WHEN p."JudgeMode" = 2 THEN p."FunctionSpecJson" ELSE NULL END,
                    p."TimeLimitMs",
                    p."MemoryLimitMb",
                    p."UpdatedAt"
                FROM "Problems" AS p
                WHERE p."IsPublished" = TRUE AND p."IsDeleted" = FALSE;

                INSERT INTO "ProblemJudgeRevisionTestCases"
                    ("Id", "ProblemJudgeRevisionId", "SourceTestCaseId", "Order", "Input", "ExpectedOutput", "ArgumentsJson", "ExpectedJson", "Visibility", "Score")
                SELECT
                    md5(p."Id"::text || ':judge-revision-test-case:1:' || t."Id"::text)::uuid,
                    md5(p."Id"::text || ':judge-revision:1')::uuid,
                    t."Id",
                    (ROW_NUMBER() OVER (PARTITION BY p."Id" ORDER BY t."CreatedAt", t."Id") - 1)::integer,
                    t."Input",
                    t."ExpectedOutput",
                    t."ArgumentsJson",
                    t."ExpectedJson",
                    t."Visibility",
                    t."Score"
                FROM "Problems" AS p
                JOIN "TestCases" AS t ON t."ProblemId" = p."Id" AND t."IsDeleted" = FALSE
                WHERE p."IsPublished" = TRUE AND p."IsDeleted" = FALSE;

                INSERT INTO "ProblemJudgeRevisionAssets"
                    ("ProblemJudgeRevisionId", "ProblemJudgeAssetId", "Order")
                SELECT
                    md5(p."Id"::text || ':judge-revision:1')::uuid,
                    a."Id",
                    (ROW_NUMBER() OVER (PARTITION BY p."Id" ORDER BY a."Language", a."OriginalFileName", a."Id") - 1)::integer
                FROM "Problems" AS p
                JOIN "ProblemJudgeAssets" AS a ON a."ProblemId" = p."Id" AND a."IsDeleted" = FALSE
                WHERE p."IsPublished" = TRUE AND p."IsDeleted" = FALSE;

                UPDATE "Problems" AS p
                SET "CurrentJudgeRevisionId" = md5(p."Id"::text || ':judge-revision:1')::uuid
                WHERE p."IsPublished" = TRUE AND p."IsDeleted" = FALSE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Problems_ProblemJudgeRevisions_CurrentJudgeRevisionId",
                table: "Problems");

            migrationBuilder.DropTable(
                name: "ProblemJudgeRevisionAssets");

            migrationBuilder.DropTable(
                name: "ProblemJudgeRevisionTestCases");

            migrationBuilder.DropTable(
                name: "ProblemJudgeRevisions");

            migrationBuilder.DropIndex(
                name: "IX_Problems_CurrentJudgeRevisionId",
                table: "Problems");

            migrationBuilder.DropIndex(
                name: "IX_ProblemJudgeAssets_ProblemId_Language_NormalizedFileName",
                table: "ProblemJudgeAssets");

            migrationBuilder.DropColumn(
                name: "CurrentJudgeRevisionId",
                table: "Problems");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ProblemJudgeAssets");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ProblemJudgeAssets");

            migrationBuilder.CreateIndex(
                name: "IX_ProblemJudgeAssets_ProblemId_Language_NormalizedFileName",
                table: "ProblemJudgeAssets",
                columns: new[] { "ProblemId", "Language", "NormalizedFileName" },
                unique: true);
        }
    }
}
