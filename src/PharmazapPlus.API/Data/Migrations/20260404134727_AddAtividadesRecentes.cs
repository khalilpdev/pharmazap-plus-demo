using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PharmazapPlus.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAtividadesRecentes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AtividadesRecentes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FarmaciaId = table.Column<int>(type: "integer", nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    UsuarioNome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TipoAtividade = table.Column<int>(type: "integer", nullable: false),
                    Descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AtendimentoId = table.Column<int>(type: "integer", nullable: true),
                    ClienteNome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DataHora = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AtividadesRecentes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AtividadesRecentes_FarmaciaId_DataHora",
                table: "AtividadesRecentes",
                columns: new[] { "FarmaciaId", "DataHora" });

            migrationBuilder.CreateIndex(
                name: "IX_AtividadesRecentes_UsuarioId_DataHora",
                table: "AtividadesRecentes",
                columns: new[] { "UsuarioId", "DataHora" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AtividadesRecentes");
        }
    }
}
