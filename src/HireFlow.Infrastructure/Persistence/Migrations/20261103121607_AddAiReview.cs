using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAiReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ai_review",
                table: "applications",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ai_review_unavailable",
                table: "applications",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ai_review_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    available_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_error = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_review_jobs", x => x.id);
                    table.ForeignKey(
                        name: "fk_ai_review_jobs_job_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_review_jobs_application_id",
                table: "ai_review_jobs",
                column: "application_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ai_review_jobs_status_available_at",
                table: "ai_review_jobs",
                columns: new[] { "status", "available_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_review_jobs");

            migrationBuilder.DropColumn(
                name: "ai_review",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "ai_review_unavailable",
                table: "applications");
        }
    }
}
