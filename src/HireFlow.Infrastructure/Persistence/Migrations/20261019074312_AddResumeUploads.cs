using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddResumeUploads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "resume_uploads",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    telegram_id = table.Column<long>(type: "bigint", nullable: false),
                    file_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resume_uploads", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_resume_uploads_telegram_id_file_hash",
                table: "resume_uploads",
                columns: new[] { "telegram_id", "file_hash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "resume_uploads");
        }
    }
}
