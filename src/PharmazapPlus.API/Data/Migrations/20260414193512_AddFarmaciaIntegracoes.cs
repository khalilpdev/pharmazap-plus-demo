using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PharmazapPlus.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFarmaciaIntegracoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create the new table first
            migrationBuilder.CreateTable(
                name: "FarmaciaIntegracoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FarmaciaId = table.Column<int>(type: "integer", nullable: false),
                    WhatsAppPhoneNumberId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    WhatsAppAccessToken = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    WhatsAppBusinessAccountId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ChaveWhatsApp = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InstagramAccountId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PageId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    InstagramAccessToken = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FarmaciaIntegracoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FarmaciaIntegracoes_Farmacias_FarmaciaId",
                        column: x => x.FarmaciaId,
                        principalTable: "Farmacias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // 2. Migrate existing data from Farmacias into FarmaciaIntegracoes
            migrationBuilder.Sql("""
                INSERT INTO "FarmaciaIntegracoes"
                    ("FarmaciaId", "WhatsAppPhoneNumberId", "WhatsAppAccessToken",
                     "WhatsAppBusinessAccountId", "ChaveWhatsApp",
                     "InstagramAccountId", "PageId", "InstagramAccessToken",
                     "CreatedAt")
                SELECT
                    "Id",
                    COALESCE("WhatsAppPhoneNumberId", ''),
                    COALESCE("WhatsAppAccessToken", ''),
                    COALESCE("WhatsAppBusinessAccountId", ''),
                    COALESCE(NULLIF("ChaveWhatsApp", ''), 'migrated-' || "Id"::text),
                    "InstagramAccountId",
                    "PageId",
                    "InstagramAccessToken",
                    NOW()
                FROM "Farmacias";
                """);

            // 3. Now drop the credential columns from Farmacias
            migrationBuilder.DropIndex(
                name: "IX_Farmacias_ChaveWhatsApp",
                table: "Farmacias");

            migrationBuilder.DropColumn(
                name: "ChaveWhatsApp",
                table: "Farmacias");

            migrationBuilder.DropColumn(
                name: "InstagramAccessToken",
                table: "Farmacias");

            migrationBuilder.DropColumn(
                name: "InstagramAccountId",
                table: "Farmacias");

            migrationBuilder.DropColumn(
                name: "PageId",
                table: "Farmacias");

            migrationBuilder.DropColumn(
                name: "WhatsAppAccessToken",
                table: "Farmacias");

            migrationBuilder.DropColumn(
                name: "WhatsAppBusinessAccountId",
                table: "Farmacias");

            migrationBuilder.DropColumn(
                name: "WhatsAppPhoneNumberId",
                table: "Farmacias");

            migrationBuilder.CreateIndex(
                name: "IX_FarmaciaIntegracoes_ChaveWhatsApp",
                table: "FarmaciaIntegracoes",
                column: "ChaveWhatsApp",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FarmaciaIntegracoes_FarmaciaId",
                table: "FarmaciaIntegracoes",
                column: "FarmaciaId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FarmaciaIntegracoes");

            migrationBuilder.AddColumn<string>(
                name: "ChaveWhatsApp",
                table: "Farmacias",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InstagramAccessToken",
                table: "Farmacias",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstagramAccountId",
                table: "Farmacias",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PageId",
                table: "Farmacias",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppAccessToken",
                table: "Farmacias",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppBusinessAccountId",
                table: "Farmacias",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppPhoneNumberId",
                table: "Farmacias",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Farmacias_ChaveWhatsApp",
                table: "Farmacias",
                column: "ChaveWhatsApp",
                unique: true);
        }
    }
}
