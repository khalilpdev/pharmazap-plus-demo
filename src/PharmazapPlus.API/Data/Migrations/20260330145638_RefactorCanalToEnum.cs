using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmazapPlus.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class RefactorCanalToEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add new integer column with default 0 (WhatsApp)
            migrationBuilder.AddColumn<int>(
                name: "CanalEnum",
                table: "Atendimentos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // 2. Migrate existing string data to enum int values
            migrationBuilder.Sql("""
                UPDATE "Atendimentos"
                SET "CanalEnum" = CASE "Canal"
                    WHEN 'Instagram' THEN 1
                    WHEN 'Telegram'  THEN 2
                    WHEN 'Messenger' THEN 3
                    ELSE 0
                END;
                """);

            // 3. Drop old string column
            migrationBuilder.DropColumn(
                name: "Canal",
                table: "Atendimentos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 1. Restore string column
            migrationBuilder.AddColumn<string>(
                name: "Canal",
                table: "Atendimentos",
                type: "text",
                nullable: false,
                defaultValue: "WhatsApp");

            // 2. Restore string data from enum int values
            migrationBuilder.Sql("""
                UPDATE "Atendimentos"
                SET "Canal" = CASE "CanalEnum"
                    WHEN 1 THEN 'Instagram'
                    WHEN 2 THEN 'Telegram'
                    WHEN 3 THEN 'Messenger'
                    ELSE 'WhatsApp'
                END;
                """);

            // 3. Drop the enum column
            migrationBuilder.DropColumn(
                name: "CanalEnum",
                table: "Atendimentos");
        }
    }
}
