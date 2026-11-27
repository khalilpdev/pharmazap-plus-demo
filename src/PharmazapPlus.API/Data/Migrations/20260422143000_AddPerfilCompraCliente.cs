using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmazapPlus.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPerfilCompraCliente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Adiciona a coluna PerfilCompra caso não exista (idempotente para bancos que
            // rodaram a migration 20260413164303_AddPerfilCompraAndValorVenda quando ela
            // ainda tinha o corpo vazio, sem criar a coluna).
            migrationBuilder.Sql(@"ALTER TABLE ""Clientes"" ADD COLUMN IF NOT EXISTS ""PerfilCompra"" integer NOT NULL DEFAULT 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PerfilCompra",
                table: "Clientes");
        }
    }
}
