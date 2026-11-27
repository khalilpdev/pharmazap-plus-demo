using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PharmazapPlus.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Farmacias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Cnpj = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Endereco = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Cidade = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Estado = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Cep = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    WhatsAppPhoneNumberId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    WhatsAppAccessToken = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    WhatsAppBusinessAccountId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ChaveWhatsApp = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Plano = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DataCadastro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    DataAtivacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DataSuspensao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DataCancelamento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Ativa = table.Column<bool>(type: "boolean", nullable: false),
                    LimiteAtendentes = table.Column<int>(type: "integer", nullable: false),
                    LimiteClientes = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Farmacias", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Clientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FarmaciaId = table.Column<int>(type: "integer", nullable: false),
                    CodigoCliente = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    WhatsAppId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Sobrenome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Apelido = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Telefone2 = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Cpf = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    Rg = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DataNascimento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Sexo = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    Endereco = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Complemento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Bairro = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Cidade = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Estado = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Cep = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Referencia = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UsoContinuo = table.Column<bool>(type: "boolean", nullable: false),
                    MedicamentosContinuos = table.Column<string>(type: "jsonb", nullable: false),
                    Alergias = table.Column<string>(type: "jsonb", nullable: false),
                    CondicoesMedicas = table.Column<string>(type: "jsonb", nullable: false),
                    TipoSanguineo = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    Peso = table.Column<decimal>(type: "numeric", nullable: true),
                    Altura = table.Column<decimal>(type: "numeric", nullable: true),
                    ObservacoesSaude = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CrmCrf = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Especialidade = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LocalTrabalho = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Segmento = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PontuacaoFidelidade = table.Column<int>(type: "integer", nullable: true),
                    DataCadastro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UltimoContato = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DataInativacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OrigemCadastro = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IndicadoPor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TotalAtendimentos = table.Column<int>(type: "integer", nullable: false),
                    TotalPedidos = table.Column<int>(type: "integer", nullable: false),
                    TotalGasto = table.Column<decimal>(type: "numeric", nullable: false),
                    UltimaCompra = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AceitaMarketing = table.Column<bool>(type: "boolean", nullable: false),
                    AceitaNotificacoes = table.Column<bool>(type: "boolean", nullable: false),
                    PreferenciasContato = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ObservacoesGerais = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CriadoPor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AtualizadoPor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clientes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Clientes_Farmacias_FarmaciaId",
                        column: x => x.FarmaciaId,
                        principalTable: "Farmacias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsuariosFarmacia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FarmaciaId = table.Column<int>(type: "integer", nullable: false),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PasswordSalt = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    EmailConfirmado = table.Column<bool>(type: "boolean", nullable: false),
                    DataCadastro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UltimoLogin = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DataInativacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Turno = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RecebeNotificacoes = table.Column<bool>(type: "boolean", nullable: false),
                    TotalAtendimentos = table.Column<int>(type: "integer", nullable: false),
                    AtendimentosHoje = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuariosFarmacia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsuariosFarmacia_Farmacias_FarmaciaId",
                        column: x => x.FarmaciaId,
                        principalTable: "Farmacias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClienteObservacoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClienteId = table.Column<int>(type: "integer", nullable: false),
                    Titulo = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    Prioridade = table.Column<string>(type: "text", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    DataConclusao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Concluida = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoPor = table.Column<string>(type: "text", nullable: false),
                    Responsavel = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClienteObservacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClienteObservacoes_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClienteTags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClienteId = table.Column<int>(type: "integer", nullable: false),
                    Tag = table.Column<string>(type: "text", nullable: false),
                    Cor = table.Column<string>(type: "text", nullable: false),
                    DataAdicao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AdicionadoPor = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClienteTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClienteTags_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Pedidos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClienteId = table.Column<int>(type: "integer", nullable: false),
                    FarmaciaId = table.Column<int>(type: "integer", nullable: false),
                    NumeroPedido = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DataPedido = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    DataEntrega = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TipoPedido = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FormaPagamento = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ValorTotal = table.Column<decimal>(type: "numeric", nullable: false),
                    Desconto = table.Column<decimal>(type: "numeric", nullable: false),
                    PrescricaoMedica = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Observacoes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    EnderecoEntrega = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Entregador = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pedidos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pedidos_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pedidos_Farmacias_FarmaciaId",
                        column: x => x.FarmaciaId,
                        principalTable: "Farmacias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Atendimentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FarmaciaId = table.Column<int>(type: "integer", nullable: false),
                    ClienteId = table.Column<int>(type: "integer", nullable: true),
                    AtendenteId = table.Column<int>(type: "integer", nullable: true),
                    WhatsAppId = table.Column<string>(type: "text", nullable: false),
                    Canal = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    DataInicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    DataFim = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DuracaoSegundos = table.Column<int>(type: "integer", nullable: true),
                    Avaliacao = table.Column<int>(type: "integer", nullable: true),
                    ComentarioAvaliacao = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Atendimentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Atendimentos_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Atendimentos_Farmacias_FarmaciaId",
                        column: x => x.FarmaciaId,
                        principalTable: "Farmacias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Atendimentos_UsuariosFarmacia_AtendenteId",
                        column: x => x.AtendenteId,
                        principalTable: "UsuariosFarmacia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PedidoItens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PedidoId = table.Column<int>(type: "integer", nullable: false),
                    ProdutoCodigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProdutoNome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProdutoDescricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Quantidade = table.Column<int>(type: "integer", nullable: false),
                    PrecoUnitario = table.Column<decimal>(type: "numeric", nullable: false),
                    Desconto = table.Column<decimal>(type: "numeric", nullable: false),
                    Dosagem = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Posologia = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Observacoes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PedidoItens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PedidoItens_Pedidos_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgendamentosRecorrentes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClienteId = table.Column<int>(type: "integer", nullable: false),
                    FarmaciaId = table.Column<int>(type: "integer", nullable: false),
                    AtendimentoId = table.Column<int>(type: "integer", nullable: true),
                    Medicamento = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProximoDisparo = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FrequenciaDias = table.Column<int>(type: "integer", nullable: false),
                    Dosagem = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Posologia = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    NotificarCliente = table.Column<bool>(type: "boolean", nullable: false),
                    NotificarAtendente = table.Column<bool>(type: "boolean", nullable: false),
                    HorarioPreferencial = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    QuantidadeDisparos = table.Column<int>(type: "integer", nullable: false),
                    UltimoDisparo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    DataCancelamento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CriadoPor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Observacoes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgendamentosRecorrentes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgendamentosRecorrentes_Atendimentos_AtendimentoId",
                        column: x => x.AtendimentoId,
                        principalTable: "Atendimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AgendamentosRecorrentes_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgendamentosRecorrentes_Farmacias_FarmaciaId",
                        column: x => x.FarmaciaId,
                        principalTable: "Farmacias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Mensagens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    MessageId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AtendimentoId = table.Column<int>(type: "integer", nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    MediaUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MediaType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    MediaCaption = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MediaSize = table.Column<long>(type: "bigint", nullable: true),
                    TemplateName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TemplateLanguage = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    TemplateVariables = table.Column<string>(type: "text", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ErrorCode = table.Column<int>(type: "integer", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mensagens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mensagens_Atendimentos_AtendimentoId",
                        column: x => x.AtendimentoId,
                        principalTable: "Atendimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgendamentosRecorrentes_AtendimentoId",
                table: "AgendamentosRecorrentes",
                column: "AtendimentoId");

            migrationBuilder.CreateIndex(
                name: "IX_AgendamentosRecorrentes_ClienteId_Ativo",
                table: "AgendamentosRecorrentes",
                columns: new[] { "ClienteId", "Ativo" });

            migrationBuilder.CreateIndex(
                name: "IX_AgendamentosRecorrentes_FarmaciaId",
                table: "AgendamentosRecorrentes",
                column: "FarmaciaId");

            migrationBuilder.CreateIndex(
                name: "IX_AgendamentosRecorrentes_ProximoDisparo",
                table: "AgendamentosRecorrentes",
                column: "ProximoDisparo");

            migrationBuilder.CreateIndex(
                name: "IX_Atendimentos_AtendenteId",
                table: "Atendimentos",
                column: "AtendenteId");

            migrationBuilder.CreateIndex(
                name: "IX_Atendimentos_ClienteId",
                table: "Atendimentos",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Atendimentos_FarmaciaId_WhatsAppId",
                table: "Atendimentos",
                columns: new[] { "FarmaciaId", "WhatsAppId" });

            migrationBuilder.CreateIndex(
                name: "IX_Atendimentos_Status",
                table: "Atendimentos",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ClienteObservacoes_ClienteId",
                table: "ClienteObservacoes",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_ClienteObservacoes_Concluida",
                table: "ClienteObservacoes",
                column: "Concluida");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_FarmaciaId_Cpf",
                table: "Clientes",
                columns: new[] { "FarmaciaId", "Cpf" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_FarmaciaId_Email",
                table: "Clientes",
                columns: new[] { "FarmaciaId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_FarmaciaId_WhatsAppId",
                table: "Clientes",
                columns: new[] { "FarmaciaId", "WhatsAppId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClienteTags_ClienteId_Tag",
                table: "ClienteTags",
                columns: new[] { "ClienteId", "Tag" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Farmacias_ChaveWhatsApp",
                table: "Farmacias",
                column: "ChaveWhatsApp",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Farmacias_Cnpj",
                table: "Farmacias",
                column: "Cnpj",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mensagens_AtendimentoId",
                table: "Mensagens",
                column: "AtendimentoId");

            migrationBuilder.CreateIndex(
                name: "IX_Mensagens_MessageId",
                table: "Mensagens",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mensagens_ReceivedAt",
                table: "Mensagens",
                column: "ReceivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PedidoItens_PedidoId",
                table: "PedidoItens",
                column: "PedidoId");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_ClienteId",
                table: "Pedidos",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_FarmaciaId_ClienteId",
                table: "Pedidos",
                columns: new[] { "FarmaciaId", "ClienteId" });

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_NumeroPedido",
                table: "Pedidos",
                column: "NumeroPedido",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_Status",
                table: "Pedidos",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_UsuariosFarmacia_FarmaciaId_Email",
                table: "UsuariosFarmacia",
                columns: new[] { "FarmaciaId", "Email" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgendamentosRecorrentes");

            migrationBuilder.DropTable(
                name: "ClienteObservacoes");

            migrationBuilder.DropTable(
                name: "ClienteTags");

            migrationBuilder.DropTable(
                name: "Mensagens");

            migrationBuilder.DropTable(
                name: "PedidoItens");

            migrationBuilder.DropTable(
                name: "Atendimentos");

            migrationBuilder.DropTable(
                name: "Pedidos");

            migrationBuilder.DropTable(
                name: "UsuariosFarmacia");

            migrationBuilder.DropTable(
                name: "Clientes");

            migrationBuilder.DropTable(
                name: "Farmacias");
        }
    }
}
