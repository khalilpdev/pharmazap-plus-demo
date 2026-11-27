using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PharmazapPlus.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEtiquetas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EtiquetaId",
                table: "Clientes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EtiquetaId",
                table: "Atendimentos",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Etiquetas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FarmaciaId = table.Column<int>(type: "integer", nullable: false),
                    Nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Cor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Etiquetas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Etiquetas_Farmacias_FarmaciaId",
                        column: x => x.FarmaciaId,
                        principalTable: "Farmacias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_EtiquetaId",
                table: "Clientes",
                column: "EtiquetaId");

            migrationBuilder.CreateIndex(
                name: "IX_Atendimentos_EtiquetaId",
                table: "Atendimentos",
                column: "EtiquetaId");

            migrationBuilder.CreateIndex(
                name: "IX_Etiquetas_FarmaciaId_Ativo",
                table: "Etiquetas",
                columns: new[] { "FarmaciaId", "Ativo" });

            migrationBuilder.CreateIndex(
                name: "IX_Etiquetas_FarmaciaId_Nome",
                table: "Etiquetas",
                columns: new[] { "FarmaciaId", "Nome" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Atendimentos_Etiquetas_EtiquetaId",
                table: "Atendimentos",
                column: "EtiquetaId",
                principalTable: "Etiquetas",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Clientes_Etiquetas_EtiquetaId",
                table: "Clientes",
                column: "EtiquetaId",
                principalTable: "Etiquetas",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Atendimentos_Etiquetas_EtiquetaId",
                table: "Atendimentos");

            migrationBuilder.DropForeignKey(
                name: "FK_Clientes_Etiquetas_EtiquetaId",
                table: "Clientes");

            migrationBuilder.DropTable(
                name: "Etiquetas");

            migrationBuilder.DropIndex(
                name: "IX_Clientes_EtiquetaId",
                table: "Clientes");

            migrationBuilder.DropIndex(
                name: "IX_Atendimentos_EtiquetaId",
                table: "Atendimentos");

            migrationBuilder.DropColumn(
                name: "EtiquetaId",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "EtiquetaId",
                table: "Atendimentos");
        }
    }
}
