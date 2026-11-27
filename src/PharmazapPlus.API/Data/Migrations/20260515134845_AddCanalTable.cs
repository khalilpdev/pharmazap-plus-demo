using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PharmazapPlus.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCanalTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Canais",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FarmaciaIntegracosId = table.Column<int>(type: "integer", nullable: false),
                    Nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    IsOfficial = table.Column<bool>(type: "boolean", nullable: false),
                    PhoneNumberId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BusinessAccountId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ApiUrl = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ApiKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InstagramAccountId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AccessToken = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Canais", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Canais_FarmaciaIntegracoes_FarmaciaIntegracosId",
                        column: x => x.FarmaciaIntegracosId,
                        principalTable: "FarmaciaIntegracoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Canais_FarmaciaIntegracosId",
                table: "Canais",
                column: "FarmaciaIntegracosId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Canais");
        }
    }
}
