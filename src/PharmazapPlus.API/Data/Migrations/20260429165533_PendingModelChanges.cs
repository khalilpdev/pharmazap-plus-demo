using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmazapPlus.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class PendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"Pedidos\" ALTER COLUMN \"TipoPedido\" TYPE integer USING CASE WHEN \"TipoPedido\" = 'Delivery' THEN 0 WHEN \"TipoPedido\" = 'Balcao' THEN 1 WHEN \"TipoPedido\" = 'WhatsApp' THEN 2 ELSE 0 END;");

            migrationBuilder.Sql("ALTER TABLE \"Pedidos\" ALTER COLUMN \"Status\" TYPE integer USING CASE WHEN \"Status\" = 'Pendente' THEN 0 WHEN \"Status\" = 'Confirmado' THEN 1 WHEN \"Status\" = 'EmPreparo' THEN 2 WHEN \"Status\" = 'Pronto' THEN 3 WHEN \"Status\" = 'SaiuParaEntrega' THEN 4 WHEN \"Status\" = 'Entregue' THEN 5 WHEN \"Status\" = 'Cancelado' THEN 6 ELSE 0 END;");

            migrationBuilder.Sql("ALTER TABLE \"Pedidos\" ALTER COLUMN \"FormaPagamento\" TYPE integer USING CASE WHEN \"FormaPagamento\" = 'NaoEspecificado' THEN 0 WHEN \"FormaPagamento\" = 'Dinheiro' THEN 1 WHEN \"FormaPagamento\" = 'CartaoCredito' THEN 2 WHEN \"FormaPagamento\" = 'CartaoDebito' THEN 3 WHEN \"FormaPagamento\" = 'Pix' THEN 4 WHEN \"FormaPagamento\" = 'Transferencia' THEN 5 WHEN \"FormaPagamento\" = 'Boleto' THEN 6 WHEN \"FormaPagamento\" = 'Crediario' THEN 7 ELSE 0 END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"Pedidos\" ALTER COLUMN \"TipoPedido\" TYPE character varying(20) USING CASE WHEN \"TipoPedido\" = 0 THEN 'Delivery'::text WHEN \"TipoPedido\" = 1 THEN 'Balcao'::text WHEN \"TipoPedido\" = 2 THEN 'WhatsApp'::text ELSE 'Delivery'::text END;");

            migrationBuilder.Sql("ALTER TABLE \"Pedidos\" ALTER COLUMN \"Status\" TYPE character varying(20) USING CASE WHEN \"Status\" = 0 THEN 'Pendente'::text WHEN \"Status\" = 1 THEN 'Confirmado'::text WHEN \"Status\" = 2 THEN 'EmPreparo'::text WHEN \"Status\" = 3 THEN 'Pronto'::text WHEN \"Status\" = 4 THEN 'SaiuParaEntrega'::text WHEN \"Status\" = 5 THEN 'Entregue'::text WHEN \"Status\" = 6 THEN 'Cancelado'::text ELSE 'Pendente'::text END;");

            migrationBuilder.Sql("ALTER TABLE \"Pedidos\" ALTER COLUMN \"FormaPagamento\" TYPE character varying(50) USING CASE WHEN \"FormaPagamento\" = 0 THEN 'NaoEspecificado'::text WHEN \"FormaPagamento\" = 1 THEN 'Dinheiro'::text WHEN \"FormaPagamento\" = 2 THEN 'CartaoCredito'::text WHEN \"FormaPagamento\" = 3 THEN 'CartaoDebito'::text WHEN \"FormaPagamento\" = 4 THEN 'Pix'::text WHEN \"FormaPagamento\" = 5 THEN 'Transferencia'::text WHEN \"FormaPagamento\" = 6 THEN 'Boleto'::text WHEN \"FormaPagamento\" = 7 THEN 'Crediario'::text ELSE 'NaoEspecificado'::text END;");
        }
    }
}
