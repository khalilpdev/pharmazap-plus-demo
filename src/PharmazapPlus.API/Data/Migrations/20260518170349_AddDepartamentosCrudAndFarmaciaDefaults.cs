using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PharmazapPlus.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartamentosCrudAndFarmaciaDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DepartamentoPadraoId",
                table: "Farmacias",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Departamentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FarmaciaId = table.Column<int>(type: "integer", nullable: false),
                    Nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departamentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Departamentos_Farmacias_FarmaciaId",
                        column: x => x.FarmaciaId,
                        principalTable: "Farmacias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Farmacias_DepartamentoPadraoId",
                table: "Farmacias",
                column: "DepartamentoPadraoId");

            migrationBuilder.CreateIndex(
                name: "IX_Departamentos_FarmaciaId_Ativo",
                table: "Departamentos",
                columns: new[] { "FarmaciaId", "Ativo" });

            migrationBuilder.CreateIndex(
                name: "IX_Departamentos_FarmaciaId_Nome",
                table: "Departamentos",
                columns: new[] { "FarmaciaId", "Nome" },
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO "Departamentos" ("FarmaciaId", "Nome", "Ativo", "CreatedAt", "UpdatedAt")
                SELECT f."Id", 'Atendimento', TRUE, NOW(), NULL
                FROM "Farmacias" f
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "Departamentos" d
                    WHERE d."FarmaciaId" = f."Id"
                      AND d."Nome" = 'Atendimento'
                );
                """);

            migrationBuilder.Sql("""
                UPDATE "Farmacias" f
                SET "DepartamentoPadraoId" = d."Id"
                FROM "Departamentos" d
                WHERE d."FarmaciaId" = f."Id"
                  AND d."Nome" = 'Atendimento'
                  AND f."DepartamentoPadraoId" IS NULL;
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_Farmacias_Departamentos_DepartamentoPadraoId",
                table: "Farmacias",
                column: "DepartamentoPadraoId",
                principalTable: "Departamentos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Farmacias_Departamentos_DepartamentoPadraoId",
                table: "Farmacias");

            migrationBuilder.DropTable(
                name: "Departamentos");

            migrationBuilder.DropIndex(
                name: "IX_Farmacias_DepartamentoPadraoId",
                table: "Farmacias");

            migrationBuilder.DropColumn(
                name: "DepartamentoPadraoId",
                table: "Farmacias");
        }
    }
}
