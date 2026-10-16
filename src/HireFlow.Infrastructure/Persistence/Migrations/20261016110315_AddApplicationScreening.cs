using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationScreening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "screening",
                table: "applications",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.Sql("ALTER TABLE applications ALTER COLUMN screening DROP DEFAULT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "screening",
                table: "applications");
        }
    }
}
