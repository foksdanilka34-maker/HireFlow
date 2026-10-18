using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBotSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bot_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    telegram_id = table.Column<long>(type: "bigint", nullable: false),
                    step = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    linked_vacancy_id = table.Column<Guid>(type: "uuid", nullable: true),
                    draft = table.Column<string>(type: "jsonb", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bot_sessions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bot_sessions_telegram_id",
                table: "bot_sessions",
                column: "telegram_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bot_sessions");
        }
    }
}
