using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmazapPlus.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoveDepartamentoToUsuarioFarmacia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DepartamentoId",
                table: "UsuariosFarmacia",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "UsuariosFarmacia" u
                SET "DepartamentoId" = f."DepartamentoPadraoId"
                FROM "Farmacias" f
                WHERE u."FarmaciaId" = f."Id"
                  AND u."Role" = 'Atendente'
                  AND u."DepartamentoId" IS NULL
                  AND f."DepartamentoPadraoId" IS NOT NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Farmacias_Departamentos_DepartamentoPadraoId",
                table: "Farmacias");

            migrationBuilder.DropIndex(
                name: "IX_Farmacias_DepartamentoPadraoId",
                table: "Farmacias");

            migrationBuilder.DropColumn(
                name: "DepartamentoPadraoId",
                table: "Farmacias");

            migrationBuilder.CreateIndex(
                name: "IX_UsuariosFarmacia_DepartamentoId",
                table: "UsuariosFarmacia",
                column: "DepartamentoId");

            migrationBuilder.AddForeignKey(
                name: "FK_UsuariosFarmacia_Departamentos_DepartamentoId",
                table: "UsuariosFarmacia",
                column: "DepartamentoId",
                principalTable: "Departamentos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UsuariosFarmacia_Departamentos_DepartamentoId",
                table: "UsuariosFarmacia");

            migrationBuilder.DropIndex(
                name: "IX_UsuariosFarmacia_DepartamentoId",
                table: "UsuariosFarmacia");

            migrationBuilder.DropColumn(
                name: "DepartamentoId",
                table: "UsuariosFarmacia");

            migrationBuilder.AddColumn<int>(
                name: "DepartamentoPadraoId",
                table: "Farmacias",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Farmacias" f
                SET "DepartamentoPadraoId" = origem."DepartamentoId"
                FROM (
                    SELECT DISTINCT ON ("FarmaciaId") "FarmaciaId", "DepartamentoId"
                    FROM "UsuariosFarmacia"
                    WHERE "Role" = 'Atendente'
                      AND "DepartamentoId" IS NOT NULL
                    ORDER BY "FarmaciaId", "Id"
                ) AS origem
                WHERE f."Id" = origem."FarmaciaId";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Farmacias_DepartamentoPadraoId",
                table: "Farmacias",
                column: "DepartamentoPadraoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Farmacias_Departamentos_DepartamentoPadraoId",
                table: "Farmacias",
                column: "DepartamentoPadraoId",
                principalTable: "Departamentos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
