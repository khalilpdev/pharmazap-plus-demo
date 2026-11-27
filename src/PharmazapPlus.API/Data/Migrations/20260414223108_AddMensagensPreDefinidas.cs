using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PharmazapPlus.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMensagensPreDefinidas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MensagensPreDefinidas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FarmaciaId = table.Column<int>(type: "integer", nullable: false),
                    Titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Texto = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MensagensPreDefinidas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MensagensPreDefinidas_Farmacias_FarmaciaId",
                        column: x => x.FarmaciaId,
                        principalTable: "Farmacias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MensagensPreDefinidas_FarmaciaId_Ativo",
                table: "MensagensPreDefinidas",
                columns: new[] { "FarmaciaId", "Ativo" });

            migrationBuilder.CreateIndex(
                name: "IX_MensagensPreDefinidas_FarmaciaId_Titulo",
                table: "MensagensPreDefinidas",
                columns: new[] { "FarmaciaId", "Titulo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MensagensPreDefinidas");
        }
    }
}
