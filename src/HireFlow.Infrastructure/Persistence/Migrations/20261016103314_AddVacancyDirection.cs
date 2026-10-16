using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVacancyDirection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "direction",
                table: "vacancies",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("ALTER TABLE vacancies ALTER COLUMN direction DROP DEFAULT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "direction",
                table: "vacancies");
        }
    }
}
