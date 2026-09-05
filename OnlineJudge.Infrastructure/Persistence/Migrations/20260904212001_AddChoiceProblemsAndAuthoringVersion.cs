using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineJudge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChoiceProblemsAndAuthoringVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "SourceCode",
                table: "Submissions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "Language",
                table: "Submissions",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "SubmissionKind",
                table: "Submissions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AlterColumn<int>(
                name: "TimeLimitMs",
                table: "Problems",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "MemoryLimitMb",
                table: "Problems",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "JudgeMode",
                table: "Problems",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);

            migrationBuilder.AddColumn<long>(
                name: "AuthoringVersion",
                table: "Problems",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ChoiceAnswerRevealAt",
                table: "Problems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChoiceAnswerRevealPolicy",
                table: "Problems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProblemKind",
                table: "Problems",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AlterColumn<int>(
                name: "TimeLimitMs",
                table: "ProblemJudgeRevisions",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "MemoryLimitMb",
                table: "ProblemJudgeRevisions",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "JudgeMode",
                table: "ProblemJudgeRevisions",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "ProblemKind",
                table: "ProblemJudgeRevisions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "ProblemChoiceQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProblemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    StemMarkdown = table.Column<string>(type: "text", nullable: false),
                    SelectionMode = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    ExplanationMarkdown = table.Column<string>(type: "text", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProblemChoiceQuestions", x => x.Id);
                    table.CheckConstraint("CK_ProblemChoiceQuestions_Mode", "\"SelectionMode\" IN (1, 2)");
                    table.CheckConstraint("CK_ProblemChoiceQuestions_Order", "\"Order\" >= 0");
                    table.CheckConstraint("CK_ProblemChoiceQuestions_Score", "\"Score\" BETWEEN 1 AND 1000");
                    table.ForeignKey(
                        name: "FK_ProblemChoiceQuestions_Problems_ProblemId",
                        column: x => x.ProblemId,
                        principalTable: "Problems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProblemJudgeRevisionChoiceQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProblemJudgeRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceQuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    StemMarkdown = table.Column<string>(type: "text", nullable: false),
                    SelectionMode = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    ExplanationMarkdown = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProblemJudgeRevisionChoiceQuestions", x => x.Id);
                    table.CheckConstraint("CK_ProblemJudgeRevisionChoiceQuestions_Mode", "\"SelectionMode\" IN (1, 2)");
                    table.CheckConstraint("CK_ProblemJudgeRevisionChoiceQuestions_Order", "\"Order\" >= 0");
                    table.CheckConstraint("CK_ProblemJudgeRevisionChoiceQuestions_Score", "\"Score\" BETWEEN 1 AND 1000");
                    table.ForeignKey(
                        name: "FK_ProblemJudgeRevisionChoiceQuestions_ProblemJudgeRevisions_P~",
                        column: x => x.ProblemJudgeRevisionId,
                        principalTable: "ProblemJudgeRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProblemChoiceOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    ContentMarkdown = table.Column<string>(type: "text", nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProblemChoiceOptions", x => x.Id);
                    table.CheckConstraint("CK_ProblemChoiceOptions_Order", "\"Order\" >= 0");
                    table.ForeignKey(
                        name: "FK_ProblemChoiceOptions_ProblemChoiceQuestions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "ProblemChoiceQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProblemJudgeRevisionChoiceOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionQuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceOptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    ContentMarkdown = table.Column<string>(type: "text", nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProblemJudgeRevisionChoiceOptions", x => x.Id);
                    table.CheckConstraint("CK_ProblemJudgeRevisionChoiceOptions_Order", "\"Order\" >= 0");
                    table.ForeignKey(
                        name: "FK_ProblemJudgeRevisionChoiceOptions_ProblemJudgeRevisionChoic~",
                        column: x => x.RevisionQuestionId,
                        principalTable: "ProblemJudgeRevisionChoiceQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubmissionChoiceQuestionResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionQuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmissionChoiceQuestionResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubmissionChoiceQuestionResults_ProblemJudgeRevisionChoiceQ~",
                        column: x => x.RevisionQuestionId,
                        principalTable: "ProblemJudgeRevisionChoiceQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubmissionChoiceQuestionResults_Submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "Submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubmissionChoiceSelections",
                columns: table => new
                {
                    QuestionResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionOptionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmissionChoiceSelections", x => new { x.QuestionResultId, x.RevisionOptionId });
                    table.ForeignKey(
                        name: "FK_SubmissionChoiceSelections_ProblemJudgeRevisionChoiceOption~",
                        column: x => x.RevisionOptionId,
                        principalTable: "ProblemJudgeRevisionChoiceOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubmissionChoiceSelections_SubmissionChoiceQuestionResults_~",
                        column: x => x.QuestionResultId,
                        principalTable: "SubmissionChoiceQuestionResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Submissions_KindPayload",
                table: "Submissions",
                sql: "(\"SubmissionKind\" = 1 AND \"Language\" IS NOT NULL AND \"SourceCode\" IS NOT NULL) OR (\"SubmissionKind\" = 2 AND \"Language\" IS NULL AND \"SourceCode\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Problems_AuthoringVersion",
                table: "Problems",
                sql: "\"AuthoringVersion\" >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Problems_KindConfiguration",
                table: "Problems",
                sql: "(\"ProblemKind\" = 1 AND \"JudgeMode\" IN (1, 2) AND \"TimeLimitMs\" IS NOT NULL AND \"MemoryLimitMb\" IS NOT NULL AND \"ChoiceAnswerRevealPolicy\" IS NULL AND \"ChoiceAnswerRevealAt\" IS NULL) OR (\"ProblemKind\" = 2 AND \"JudgeMode\" IS NULL AND \"TimeLimitMs\" IS NULL AND \"MemoryLimitMb\" IS NULL AND \"AllowedLanguagesMask\" = 0 AND \"FunctionSpecJson\" IS NULL AND \"StarterCodeJson\" IS NULL AND ((\"ChoiceAnswerRevealPolicy\" IS NULL AND \"ChoiceAnswerRevealAt\" IS NULL) OR (\"ChoiceAnswerRevealPolicy\" = 1 AND \"ChoiceAnswerRevealAt\" IS NULL) OR (\"ChoiceAnswerRevealPolicy\" = 2 AND \"ChoiceAnswerRevealAt\" IS NOT NULL)))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProblemJudgeRevisions_KindConfiguration",
                table: "ProblemJudgeRevisions",
                sql: "(\"ProblemKind\" = 1 AND \"JudgeMode\" IN (1, 2) AND \"TimeLimitMs\" IS NOT NULL AND \"MemoryLimitMb\" IS NOT NULL) OR (\"ProblemKind\" = 2 AND \"JudgeMode\" IS NULL AND \"TimeLimitMs\" IS NULL AND \"MemoryLimitMb\" IS NULL AND \"AllowedLanguagesMask\" = 0 AND \"FunctionSpecJson\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_ProblemChoiceOptions_QuestionId_Order",
                table: "ProblemChoiceOptions",
                columns: new[] { "QuestionId", "Order" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ProblemChoiceQuestions_ProblemId_Order",
                table: "ProblemChoiceQuestions",
                columns: new[] { "ProblemId", "Order" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ProblemJudgeRevisionChoiceOptions_RevisionQuestionId_Order",
                table: "ProblemJudgeRevisionChoiceOptions",
                columns: new[] { "RevisionQuestionId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProblemJudgeRevisionChoiceOptions_RevisionQuestionId_Source~",
                table: "ProblemJudgeRevisionChoiceOptions",
                columns: new[] { "RevisionQuestionId", "SourceOptionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProblemJudgeRevisionChoiceQuestions_ProblemJudgeRevisionId_~",
                table: "ProblemJudgeRevisionChoiceQuestions",
                columns: new[] { "ProblemJudgeRevisionId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProblemJudgeRevisionChoiceQuestions_ProblemJudgeRevisionId~1",
                table: "ProblemJudgeRevisionChoiceQuestions",
                columns: new[] { "ProblemJudgeRevisionId", "SourceQuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionChoiceQuestionResults_RevisionQuestionId",
                table: "SubmissionChoiceQuestionResults",
                column: "RevisionQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionChoiceQuestionResults_SubmissionId_RevisionQuesti~",
                table: "SubmissionChoiceQuestionResults",
                columns: new[] { "SubmissionId", "RevisionQuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionChoiceSelections_RevisionOptionId",
                table: "SubmissionChoiceSelections",
                column: "RevisionOptionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProblemChoiceOptions");

            migrationBuilder.DropTable(
                name: "SubmissionChoiceSelections");

            migrationBuilder.DropTable(
                name: "ProblemChoiceQuestions");

            migrationBuilder.DropTable(
                name: "ProblemJudgeRevisionChoiceOptions");

            migrationBuilder.DropTable(
                name: "SubmissionChoiceQuestionResults");

            migrationBuilder.DropTable(
                name: "ProblemJudgeRevisionChoiceQuestions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Submissions_KindPayload",
                table: "Submissions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Problems_AuthoringVersion",
                table: "Problems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Problems_KindConfiguration",
                table: "Problems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProblemJudgeRevisions_KindConfiguration",
                table: "ProblemJudgeRevisions");

            migrationBuilder.DropColumn(
                name: "SubmissionKind",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "AuthoringVersion",
                table: "Problems");

            migrationBuilder.DropColumn(
                name: "ChoiceAnswerRevealAt",
                table: "Problems");

            migrationBuilder.DropColumn(
                name: "ChoiceAnswerRevealPolicy",
                table: "Problems");

            migrationBuilder.DropColumn(
                name: "ProblemKind",
                table: "Problems");

            migrationBuilder.DropColumn(
                name: "ProblemKind",
                table: "ProblemJudgeRevisions");

            migrationBuilder.AlterColumn<string>(
                name: "SourceCode",
                table: "Submissions",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Language",
                table: "Submissions",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TimeLimitMs",
                table: "Problems",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "MemoryLimitMb",
                table: "Problems",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "JudgeMode",
                table: "Problems",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TimeLimitMs",
                table: "ProblemJudgeRevisions",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "MemoryLimitMb",
                table: "ProblemJudgeRevisions",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "JudgeMode",
                table: "ProblemJudgeRevisions",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
