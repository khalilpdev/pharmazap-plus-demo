using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmazapPlus.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class RelaxClienteConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tornar todos os campos opcionais da tabela Clientes nullable.
            // Apenas WhatsAppId e Nome permanecem NOT NULL (já estavam).
            var columns = new[]
            {
                "Sobrenome", "Apelido", "Email", "Telefone", "Telefone2",
                "Cpf", "Rg", "Sexo", "Endereco", "Numero", "Complemento",
                "Bairro", "Cidade", "Estado", "Cep", "Referencia",
                "TipoSanguineo", "ObservacoesSaude", "CrmCrf", "Especialidade",
                "LocalTrabalho", "OrigemCadastro", "IndicadoPor", "Segmento",
                "PreferenciasContato", "ObservacoesGerais", "CriadoPor",
                "AtualizadoPor", "CodigoCliente", "Alergias", "CondicoesMedicas",
                "MedicamentosContinuos"
            };

            foreach (var col in columns)
            {
                migrationBuilder.Sql($"ALTER TABLE \"Clientes\" ALTER COLUMN \"{col}\" DROP NOT NULL;");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reversão intencional das constraints NOT NULL não aplicada:
            // dados existentes podem ter NULLs após a migration Up.
            // Para reverter manualmente: UPDATE "Clientes" SET col = '' WHERE col IS NULL
            // e então: ALTER TABLE "Clientes" ALTER COLUMN col SET NOT NULL
        }
    }
}
