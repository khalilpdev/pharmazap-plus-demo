using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PharmazapPlus.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMotivoEncerramentoCadastro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ComprovanteUrl",
                table: "Atendimentos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MotivoEncerramentoId",
                table: "Atendimentos",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MotivosEncerramento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FarmaciaId = table.Column<int>(type: "integer", nullable: false),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExigeValorVenda = table.Column<bool>(type: "boolean", nullable: false),
                    ExigeComprovante = table.Column<bool>(type: "boolean", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MotivosEncerramento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MotivosEncerramento_Farmacias_FarmaciaId",
                        column: x => x.FarmaciaId,
                        principalTable: "Farmacias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Atendimentos_MotivoEncerramentoId",
                table: "Atendimentos",
                column: "MotivoEncerramentoId");

            migrationBuilder.CreateIndex(
                name: "IX_MotivosEncerramento_FarmaciaId_Ativo",
                table: "MotivosEncerramento",
                columns: new[] { "FarmaciaId", "Ativo" });

            migrationBuilder.CreateIndex(
                name: "IX_MotivosEncerramento_FarmaciaId_Nome",
                table: "MotivosEncerramento",
                columns: new[] { "FarmaciaId", "Nome" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Atendimentos_MotivosEncerramento_MotivoEncerramentoId",
                table: "Atendimentos",
                column: "MotivoEncerramentoId",
                principalTable: "MotivosEncerramento",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Atendimentos_MotivosEncerramento_MotivoEncerramentoId",
                table: "Atendimentos");

            migrationBuilder.DropTable(
                name: "MotivosEncerramento");

            migrationBuilder.DropIndex(
                name: "IX_Atendimentos_MotivoEncerramentoId",
                table: "Atendimentos");

            migrationBuilder.DropColumn(
                name: "ComprovanteUrl",
                table: "Atendimentos");

            migrationBuilder.DropColumn(
                name: "MotivoEncerramentoId",
                table: "Atendimentos");
        }
    }
}
