using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmazapPlus.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPerfilCompraAndValorVenda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Todas as colunas desta migration já foram adicionadas por migrations anteriores:
            // - InstagramAccessToken, InstagramAccountId, PageId → 20260326190426_AddInstagramFieldsToFarmacia
            // - PerfilCompra → 20260407132440_AddPerfilCompraCliente
            // - ValorVenda → 20260409140337_AddValorVendaAtendimento
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
