using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineJudge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHelpDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HelpDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Summary = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    MarkdownContent = table.Column<string>(type: "text", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HelpDocuments_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HelpDocuments_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HelpDocuments_CreatedByUserId",
                table: "HelpDocuments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HelpDocuments_IsPublished_SortOrder",
                table: "HelpDocuments",
                columns: new[] { "IsPublished", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_HelpDocuments_Slug",
                table: "HelpDocuments",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HelpDocuments_UpdatedByUserId",
                table: "HelpDocuments",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HelpDocuments");
        }
    }
}
