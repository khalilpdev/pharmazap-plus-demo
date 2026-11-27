using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmazapPlus.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAtendimentoIdToPedido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AtendenteId",
                table: "Pedidos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AtendimentoId",
                table: "Pedidos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppApiKey",
                table: "FarmaciaIntegracoes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppApiUrl",
                table: "FarmaciaIntegracoes",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AtendenteId",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "AtendimentoId",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "WhatsAppApiKey",
                table: "FarmaciaIntegracoes");

            migrationBuilder.DropColumn(
                name: "WhatsAppApiUrl",
                table: "FarmaciaIntegracoes");
        }
    }
}
