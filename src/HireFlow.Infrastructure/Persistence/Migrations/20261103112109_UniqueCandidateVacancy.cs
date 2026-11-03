using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UniqueCandidateVacancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_applications_candidate_id",
                table: "applications");

            migrationBuilder.CreateIndex(
                name: "ix_applications_candidate_id_vacancy_id",
                table: "applications",
                columns: new[] { "candidate_id", "vacancy_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_applications_candidate_id_vacancy_id",
                table: "applications");

            migrationBuilder.CreateIndex(
                name: "ix_applications_candidate_id",
                table: "applications",
                column: "candidate_id");
        }
    }
}
