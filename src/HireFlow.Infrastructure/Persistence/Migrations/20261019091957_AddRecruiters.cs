using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecruiters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "recruiter_id",
                table: "application_status_history",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "recruiters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    login = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recruiters", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_application_status_history_recruiter_id",
                table: "application_status_history",
                column: "recruiter_id");

            migrationBuilder.CreateIndex(
                name: "ix_recruiters_login",
                table: "recruiters",
                column: "login",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_application_status_history_recruiters_recruiter_id",
                table: "application_status_history",
                column: "recruiter_id",
                principalTable: "recruiters",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_application_status_history_recruiters_recruiter_id",
                table: "application_status_history");

            migrationBuilder.DropTable(
                name: "recruiters");

            migrationBuilder.DropIndex(
                name: "ix_application_status_history_recruiter_id",
                table: "application_status_history");

            migrationBuilder.DropColumn(
                name: "recruiter_id",
                table: "application_status_history");
        }
    }
}
