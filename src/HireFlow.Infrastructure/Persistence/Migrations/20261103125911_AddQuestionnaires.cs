using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionnaires : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ai_review_jobs_application_id",
                table: "ai_review_jobs");

            migrationBuilder.AddColumn<string>(
                name: "questions_json",
                table: "vacancies",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "answers_review",
                table: "applications",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "answers_review_unavailable",
                table: "applications",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "questions_deadline",
                table: "applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "ai_review_jobs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "resume");

            migrationBuilder.CreateTable(
                name: "application_questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    criterion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    answer = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_application_questions", x => x.id);
                    table.ForeignKey(
                        name: "fk_application_questions_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_review_jobs_application_id_kind",
                table: "ai_review_jobs",
                columns: new[] { "application_id", "kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_application_questions_application_id_position",
                table: "application_questions",
                columns: new[] { "application_id", "position" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_questions");

            migrationBuilder.DropIndex(
                name: "ix_ai_review_jobs_application_id_kind",
                table: "ai_review_jobs");

            migrationBuilder.DropColumn(
                name: "questions_json",
                table: "vacancies");

            migrationBuilder.DropColumn(
                name: "answers_review",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "answers_review_unavailable",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "questions_deadline",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "ai_review_jobs");

            migrationBuilder.CreateIndex(
                name: "ix_ai_review_jobs_application_id",
                table: "ai_review_jobs",
                column: "application_id",
                unique: true);
        }
    }
}
