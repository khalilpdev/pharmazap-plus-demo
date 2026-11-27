using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PharmazapPlus.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRemetentoBloqueado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RemetentesBlockeados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NumeroNormalizado = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    FarmaciaId = table.Column<int>(type: "integer", nullable: true),
                    Escopo = table.Column<string>(type: "text", nullable: false),
                    MotivoBloqueia = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DataBloqueio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BloqueadoPorId = table.Column<int>(type: "integer", nullable: false),
                    NomeBloqueadoPor = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    DataDesbloqueio = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DesbloqueadoPorId = table.Column<int>(type: "integer", nullable: true),
                    NomeDesbloqueadoPor = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemetentesBlockeados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemetentesBlockeados_Farmacias_FarmaciaId",
                        column: x => x.FarmaciaId,
                        principalTable: "Farmacias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RemetentesBlockeados_UsuariosFarmacia_BloqueadoPorId",
                        column: x => x.BloqueadoPorId,
                        principalTable: "UsuariosFarmacia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RemetentesBlockeados_UsuariosFarmacia_DesbloqueadoPorId",
                        column: x => x.DesbloqueadoPorId,
                        principalTable: "UsuariosFarmacia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RemetentesBlockeados_Ativo_DataBloqueio",
                table: "RemetentesBlockeados",
                columns: new[] { "Ativo", "DataBloqueio" });

            migrationBuilder.CreateIndex(
                name: "IX_RemetentesBlockeados_BloqueadoPorId",
                table: "RemetentesBlockeados",
                column: "BloqueadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_RemetentesBlockeados_DesbloqueadoPorId",
                table: "RemetentesBlockeados",
                column: "DesbloqueadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_RemetentesBlockeados_Escopo",
                table: "RemetentesBlockeados",
                column: "Escopo");

            migrationBuilder.CreateIndex(
                name: "IX_RemetentesBlockeados_FarmaciaId",
                table: "RemetentesBlockeados",
                column: "FarmaciaId");

            migrationBuilder.CreateIndex(
                name: "IX_RemetentesBlockeados_NumeroNormalizado_FarmaciaId",
                table: "RemetentesBlockeados",
                columns: new[] { "NumeroNormalizado", "FarmaciaId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RemetentesBlockeados");
        }
    }
}
