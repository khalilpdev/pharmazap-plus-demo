// PharmazapPlus.API/Data/DatabaseInitializer.cs
using Microsoft.EntityFrameworkCore;
using PharmazapPlus.API.Services.Security;

namespace PharmazapPlus.API.Data;

/// <summary>
/// Gerencia a inicialização do banco de dados: migrações e seed de dados.
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>
    /// Aplica migrações pendentes e executa seed de dados de forma idempotente.
    /// </summary>
    /// <param name="app">Instância da aplicação web</param>
    public static async Task InitializeAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PharmazapDbContext>();
        var cryptoService = scope.ServiceProvider.GetRequiredService<ICryptoService>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = app.Logger;

        try
        {
            // Verifica se o banco existe e está acessível
            var canConnect = await dbContext.Database.CanConnectAsync();
            if (!canConnect)
            {
                logger.LogWarning("⚠️ Não foi possível conectar ao banco de dados. Tentando criar...");
            }

            // Aplica migrações pendentes de forma idempotente
            var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                logger.LogInformation("📦 Aplicando {Count} migração(ões) pendente(s)...", pendingMigrations.Count());
                await dbContext.Database.MigrateAsync();
                logger.LogInformation("✅ Migrações aplicadas com sucesso.");
            }
            else
            {
                logger.LogInformation("✅ Banco de dados já está atualizado. Nenhuma migração pendente.");
            }

            // Executa seed de dados
            await DatabaseSeeder.SeedAsync(dbContext, logger, cryptoService, config);

            // Garante estruturas críticas para recursos recentes mesmo em cenários de histórico inconsistente
            await EnsureCriticalSchemaAsync(dbContext, logger);
        }
        catch (Npgsql.PostgresException pgEx) when (pgEx.SqlState == "42P07")
        {   
            // Erro 42P07: tabela já existe
            logger.LogWarning(pgEx,
                "⚠️ Detectada inconsistência entre migrações e esquema do banco. " +
                "A tabela '{TableName}' já existe. Tentando recuperar...",
                pgEx.TableName);

            await HandleMigrationInconsistencyAsync(dbContext, logger, cryptoService, app.Environment, config);
            await EnsureCriticalSchemaAsync(dbContext, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Erro ao migrar ou popular banco de dados: {Message}", ex.Message);

            try
            {
                await EnsureCriticalSchemaAsync(dbContext, logger);
            }
            catch (Exception schemaEx)
            {
                logger.LogError(schemaEx, "❌ Erro ao validar esquema crítico do banco: {Message}", schemaEx.Message);
            }

            // Em desenvolvimento, falhar rápido para evidenciar problemas de conexão/seed
            if (app.Environment.IsDevelopment())
                throw;
        }
    }

    /// <summary>
    /// Trata inconsistências entre o histórico de migrações e o esquema real do banco.
    /// </summary>
    private static async Task HandleMigrationInconsistencyAsync(
        PharmazapDbContext dbContext,
        ILogger logger,
        ICryptoService cryptoService,
        IWebHostEnvironment environment,
        IConfiguration config)
    {
        try
        {
            // Obtém todas as migrações disponíveis e as já aplicadas
            var allMigrations = dbContext.Database.GetMigrations();
            var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
            var missingMigrations = allMigrations.Except(appliedMigrations).ToList();

            if (missingMigrations.Any())
            {
                logger.LogWarning(
                    "⚠️ Encontradas {Count} migração(ões) não registrada(s) no histórico: {Migrations}",
                    missingMigrations.Count,
                    string.Join(", ", missingMigrations));

                logger.LogWarning(
                    "💡 Para corrigir definitivamente:\n" +
                    "   1. dotnet ef database drop --project PharmazapPlus.API --force\n" +
                    "   2. dotnet ef database update --project PharmazapPlus.API");
            }

            // Tenta executar o seed mesmo com inconsistência
            logger.LogInformation("🔄 Tentando executar seed de dados...");
            await DatabaseSeeder.SeedAsync(dbContext, logger, cryptoService, config);
            logger.LogInformation("✅ Seed executado com sucesso apesar da inconsistência de migração.");
        }
        catch (Exception seedEx)
        {
            logger.LogError(seedEx, "❌ Erro ao executar seed após inconsistência de migração: {Message}", seedEx.Message);

            // Em desenvolvimento, não lança exceção para permitir desenvolvimento local
            if (!environment.IsDevelopment())
                throw;
        }
    }

    /// <summary>
    /// Garante de forma idempotente o schema mínimo de Campanhas e MensagensPreDefinidas
    /// como fallback para ambientes com histórico de migrações inconsistente.
    /// </summary>
    private static async Task EnsureCriticalSchemaAsync(PharmazapDbContext dbContext, ILogger logger)
    {
        await dbContext.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS ""Campanhas"" (
    ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    ""FarmaciaId"" integer NOT NULL,
    ""Nome"" character varying(200) NOT NULL,
    ""Mensagem"" character varying(2000) NOT NULL,
    ""Tipo"" integer NOT NULL,
    ""Status"" integer NOT NULL,
    ""EnviarParaTodosAtivos"" boolean NOT NULL,
    ""EnviarParaUsoContinuo"" boolean NOT NULL,
    ""EnviarParaAceitaMarketing"" boolean NOT NULL,
    ""FiltroPerfilCompra"" jsonb NULL,
    ""FiltroEtiquetas"" jsonb NULL,
    ""DataAgendamento"" timestamp with time zone NULL,
    ""DataExecucao"" timestamp with time zone NULL,
    ""DataConclusao"" timestamp with time zone NULL,
    ""TotalDestinatarios"" integer NOT NULL,
    ""TotalEnviados"" integer NOT NULL,
    ""TotalErros"" integer NOT NULL,
    ""Observacoes"" character varying(500) NULL,
    ""CriadoPor"" character varying(100) NOT NULL,
    ""DataCriacao"" timestamp with time zone NOT NULL DEFAULT NOW(),
    ""AtualizadoEm"" timestamp with time zone NULL,
    ""AtualizadoPor"" character varying(100) NULL,
    CONSTRAINT ""FK_Campanhas_Farmacias_FarmaciaId""
        FOREIGN KEY (""FarmaciaId"") REFERENCES ""Farmacias""(""Id"") ON DELETE CASCADE
);");

        await dbContext.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS ""IX_Campanhas_DataAgendamento"" ON ""Campanhas"" (""DataAgendamento"");");
        await dbContext.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS ""IX_Campanhas_FarmaciaId_Status"" ON ""Campanhas"" (""FarmaciaId"", ""Status"");");

        await dbContext.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS ""MensagensPreDefinidas"" (
    ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    ""FarmaciaId"" integer NOT NULL,
    ""Titulo"" character varying(200) NOT NULL,
    ""Texto"" character varying(2000) NOT NULL,
    ""Ativo"" boolean NOT NULL,
    ""Ordem"" integer NOT NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    CONSTRAINT ""FK_MensagensPreDefinidas_Farmacias_FarmaciaId""
        FOREIGN KEY (""FarmaciaId"") REFERENCES ""Farmacias""(""Id"") ON DELETE CASCADE
);");

        await dbContext.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS ""IX_MensagensPreDefinidas_FarmaciaId_Ativo"" ON ""MensagensPreDefinidas"" (""FarmaciaId"", ""Ativo"");");
        await dbContext.Database.ExecuteSqlRawAsync(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_MensagensPreDefinidas_FarmaciaId_Titulo"" ON ""MensagensPreDefinidas"" (""FarmaciaId"", ""Titulo"");");

        await dbContext.Database.ExecuteSqlRawAsync(@"ALTER TABLE ""UsuariosFarmacia"" ADD COLUMN IF NOT EXISTS ""DepartamentoId"" integer NULL;");

        await dbContext.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS ""Departamentos"" (
    ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    ""FarmaciaId"" integer NOT NULL,
    ""Nome"" character varying(100) NOT NULL,
    ""Ativo"" boolean NOT NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    CONSTRAINT ""FK_Departamentos_Farmacias_FarmaciaId""
        FOREIGN KEY (""FarmaciaId"") REFERENCES ""Farmacias""(""Id"") ON DELETE CASCADE
);");

        await dbContext.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS ""IX_Departamentos_FarmaciaId_Ativo"" ON ""Departamentos"" (""FarmaciaId"", ""Ativo"");");
        await dbContext.Database.ExecuteSqlRawAsync(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Departamentos_FarmaciaId_Nome"" ON ""Departamentos"" (""FarmaciaId"", ""Nome"");");
        // Migration 20260520135732_MoveDepartamentoToUsuarioFarmacia already migrated
        // DepartamentoPadraoId from Farmacias to UsuarioFarmacia.DepartamentoId and
        // removed the column from Farmacias. No raw SQL fallback needed here.

        await dbContext.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS ""IX_UsuariosFarmacia_DepartamentoId"" ON ""UsuariosFarmacia"" (""DepartamentoId"");");
        await dbContext.Database.ExecuteSqlRawAsync(@"
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'FK_UsuariosFarmacia_Departamentos_DepartamentoId'
    ) THEN
        ALTER TABLE ""UsuariosFarmacia""
            ADD CONSTRAINT ""FK_UsuariosFarmacia_Departamentos_DepartamentoId""
            FOREIGN KEY (""DepartamentoId"") REFERENCES ""Departamentos""(""Id"") ON DELETE SET NULL;
    END IF;
END $$;");

        logger.LogInformation("✅ Esquema crítico de Campanhas, Mensagens Predefinidas e Departamentos validado.");
    }
}
