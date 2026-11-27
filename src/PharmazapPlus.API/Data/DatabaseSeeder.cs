// PharmazapPlus.API/Data/DatabaseSeeder.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using PharmazapPlus.API.Services.Security;
using PharmazapPlus.Shared;
using PharmazapPlus.Shared.Models.Core;
using PharmazapPlus.Shared.Models.CRM;
using PharmazapPlus.Shared.Models.Mensagens;
using System.Security.Cryptography;
using System.Text;

namespace PharmazapPlus.API.Data;

public static class DatabaseSeeder
{
    private const string SeedCnpj = "09717241000120";
    private const string SeedFarmaciaNome = "Farmacia Teste";
    private const string SeedFarmaciaEmail = "contato@farmaciateste.com.br";
    private const string SeedChaveWhatsApp = "farmacia-teste-key";
    private const string DepartamentoPadraoNome = "Atendimento";

    private static readonly (string Nome, string Cor)[] SeedEtiquetas =
    {
        ("VIP", Consts.EtiquetaCores.Amarelo),
        ("Urgente", Consts.EtiquetaCores.Vermelho),
        ("Retorno", Consts.EtiquetaCores.Azul),
        ("Cobrança", Consts.EtiquetaCores.Verde),
        ("Novo Cliente", Consts.EtiquetaCores.Roxo),
        ("Em Acompanhamento", Consts.EtiquetaCores.Ciano)
    };

    private static readonly (string Titulo, string Texto, int Ordem)[] SeedMensagensPreDefinidas =
    {
        ("Saudação", "Olá! Como podemos ajudar você hoje?", 1),
        ("Receita pronta", "Olá! Sua receita está pronta para retirada.", 2),
        ("Pedido confirmado", "Seu pedido foi confirmado e está em separação.", 3),
        ("Fora do horário", "No momento estamos fora do horário de atendimento. Retornaremos assim que possível.", 4)
    };

    
    public static async Task SeedAsync(PharmazapDbContext context, ILogger logger, ICryptoService cryptoService, IConfiguration? config = null)
    {
        try
        {
            var whatsAppPhoneNumberId = "";
            var whatsAppAccessToken = "";
            var evolutionApiUrl = config?["WhatsApp:NonOfficialApiUrl"]?.Trim() ?? "http://localhost:8080";
            var evolutionApiKey = config?["GLOBAL_API_KEY"]?.Trim() ?? "";

            // Garantir que a farmácia de desenvolvimento existe (idempotente)
            var farmacia = await context.Farmacias
                .Include(f => f.Integracoes)
                .FirstOrDefaultAsync(f => f.Cnpj == SeedCnpj);

            if (farmacia == null)
            {
                farmacia = await context.Farmacias
                    .Include(f => f.Integracoes)
                    .FirstOrDefaultAsync(f => f.Nome == SeedFarmaciaNome && f.Email == SeedFarmaciaEmail);

                if (farmacia != null && farmacia.Cnpj != SeedCnpj)
                {
                    farmacia.Cnpj = SeedCnpj;
                    farmacia.UpdatedAt = DateTime.UtcNow;
                    await context.SaveChangesAsync();
                    logger.LogInformation("CNPJ da farmácia de desenvolvimento atualizado para valor válido.");
                }
            }

            if (farmacia == null)
            {
                farmacia = new Farmacia
                {
                    Nome = SeedFarmaciaNome,
                    Cnpj = SeedCnpj,
                    Telefone = "(62) 3333-4444",
                    Email = SeedFarmaciaEmail,
                    Endereco = "Rua das Flores, 123 - Centro",
                    Cidade = "Goiânia",
                    Estado = "GO",
                    Cep = "74335-040",
                    Status = FarmaciaStatus.Ativa,
                    Plano = "Premium",
                    DataCadastro = DateTime.UtcNow,
                    DataAtivacao = DateTime.UtcNow,
                    Ativa = true,
                    IsActive = true,
                    LimiteAtendentes = 10,
                    LimiteClientes = 5000,
                    CreatedAt = DateTime.UtcNow
                };

                await context.Farmacias.AddAsync(farmacia);
                await context.SaveChangesAsync();
                logger.LogInformation("Farmácia de desenvolvimento criada (ID: {Id}).", farmacia.Id);

                var integracaoExistente = await context.FarmaciaIntegracoes
                    .FirstOrDefaultAsync(i => i.ChaveWhatsApp == SeedChaveWhatsApp);

                if (integracaoExistente == null)
                {
                    context.FarmaciaIntegracoes.Add(new FarmaciaIntegracoes
                    {
                        FarmaciaId = farmacia.Id,
                        WhatsAppPhoneNumberId = whatsAppPhoneNumberId,
                        WhatsAppAccessToken = whatsAppAccessToken,
                        WhatsAppBusinessAccountId = "",
                        WhatsAppApiUrl = evolutionApiUrl,
                        WhatsAppApiKey = evolutionApiKey,
                        ChaveWhatsApp = SeedChaveWhatsApp,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    integracaoExistente.FarmaciaId = farmacia.Id;
                    integracaoExistente.WhatsAppPhoneNumberId = whatsAppPhoneNumberId;
                    integracaoExistente.WhatsAppAccessToken = whatsAppAccessToken;
                    integracaoExistente.WhatsAppBusinessAccountId = "";
                    integracaoExistente.WhatsAppApiUrl = evolutionApiUrl;
                    integracaoExistente.WhatsAppApiKey = evolutionApiKey;
                    if (string.IsNullOrWhiteSpace(integracaoExistente.ChaveWhatsApp) ||
                        integracaoExistente.ChaveWhatsApp == SeedChaveWhatsApp)
                        integracaoExistente.ChaveWhatsApp = SeedChaveWhatsApp;
                    integracaoExistente.UpdatedAt = DateTime.UtcNow;
                }

                await context.SaveChangesAsync();
                logger.LogInformation("Integração de desenvolvimento garantida para a farmácia {Id}.", farmacia.Id);
            }
            else if (farmacia.Integracoes == null)
            {
                var integracaoExistente = await context.FarmaciaIntegracoes
                    .FirstOrDefaultAsync(i => i.FarmaciaId == farmacia.Id || i.ChaveWhatsApp == SeedChaveWhatsApp);

                if (integracaoExistente == null)
                {
                    farmacia.Integracoes = new FarmaciaIntegracoes
                    {
                        FarmaciaId = farmacia.Id,
                        WhatsAppPhoneNumberId = whatsAppPhoneNumberId,
                        WhatsAppAccessToken = whatsAppAccessToken,
                        WhatsAppBusinessAccountId = "",
                        WhatsAppApiUrl = evolutionApiUrl,
                        WhatsAppApiKey = evolutionApiKey,
                        ChaveWhatsApp = SeedChaveWhatsApp,
                        CreatedAt = DateTime.UtcNow
                    };
                }
                else
                {
                    integracaoExistente.FarmaciaId = farmacia.Id;
                    integracaoExistente.WhatsAppPhoneNumberId = whatsAppPhoneNumberId;
                    integracaoExistente.WhatsAppAccessToken = whatsAppAccessToken;
                    integracaoExistente.WhatsAppBusinessAccountId = "";
                    integracaoExistente.WhatsAppApiUrl = evolutionApiUrl;
                    integracaoExistente.WhatsAppApiKey = evolutionApiKey;
                    if (string.IsNullOrWhiteSpace(integracaoExistente.ChaveWhatsApp) ||
                        integracaoExistente.ChaveWhatsApp == SeedChaveWhatsApp)
                        integracaoExistente.ChaveWhatsApp = SeedChaveWhatsApp;
                    integracaoExistente.UpdatedAt = DateTime.UtcNow;
                    farmacia.Integracoes = integracaoExistente;
                }

                await context.SaveChangesAsync();
                logger.LogInformation("Integração de desenvolvimento garantida para a farmácia {Id}.", farmacia.Id);
            }
            else
            {
                farmacia.Integracoes.WhatsAppPhoneNumberId = whatsAppPhoneNumberId;
                farmacia.Integracoes.WhatsAppAccessToken = whatsAppAccessToken;
                farmacia.Integracoes.WhatsAppBusinessAccountId = "";
                farmacia.Integracoes.WhatsAppApiUrl = evolutionApiUrl;
                farmacia.Integracoes.WhatsAppApiKey = evolutionApiKey;
                if (string.IsNullOrWhiteSpace(farmacia.Integracoes.ChaveWhatsApp) ||
                    farmacia.Integracoes.ChaveWhatsApp == SeedChaveWhatsApp)
                    farmacia.Integracoes.ChaveWhatsApp = SeedChaveWhatsApp;
                farmacia.Integracoes.UpdatedAt = DateTime.UtcNow;

                await context.SaveChangesAsync();
                logger.LogInformation("Integração de desenvolvimento atualizada para a farmácia {Id}.", farmacia.Id);
            }

            // Criar cada usuário de desenvolvimento de forma idempotente
            await EnsureUserAsync(context, logger, farmacia.Id, cryptoService, new SeedUser(
                Nome: "Desenvolvedor Pharmazap",
                Email: "dev@pharmazap.com.br",
                Telefone: "(11) 96666-6666",
                Cpf: "00000000000",
                Role: UserRoles.Desenvolvedor,
                PasswordConfigKey: "Seed:DeveloperPassword",
                Turno: "Integral"
            ), config);

            await EnsureUserAsync(context, logger, farmacia.Id, cryptoService, new SeedUser(
                Nome: "Gestor Sistema",
                Email: "gestor@pharmazapplus.com",
                Telefone: "(11) 97777-7777",
                Cpf: "00000000001",
                Role: UserRoles.Gestor,
                PasswordConfigKey: "Seed:GestorPassword",
                Turno: "Integral"
            ), config);

            await EnsureUserAsync(context, logger, farmacia.Id, cryptoService, new SeedUser(
                Nome: "Administrador Sistema",
                Email: "admin@pharmazapplus.com",
                Telefone: "(11) 99999-9999",
                Cpf: "00000000002",
                Role: UserRoles.Administrador,
                PasswordConfigKey: "Seed:AdminPassword",
                Turno: "Integral"
            ), config);

            await EnsureUserAsync(context, logger, farmacia.Id, cryptoService, new SeedUser(
                Nome: "João Atendente",
                Email: "atendente@farmaciateste.com.br",
                Telefone: "(11) 98888-8888",
                Cpf: "00000000003",
                Role: UserRoles.Atendente,
                PasswordConfigKey: "Seed:AttendantPassword",
                Turno: "Manhã"
            ), config);

            var farmaciaIds = await context.Farmacias
                .Select(f => f.Id)
                .ToListAsync();

            foreach (var farmaciaId in farmaciaIds)
            {
                await EnsureFarmaciaDefaultsAsync(context, logger, farmaciaId);
            }

            logger.LogInformation("Seed de dados de desenvolvimento concluído.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao popular banco de dados");
            throw;
        }
    }

    public static async Task EnsureFarmaciaDefaultsAsync(PharmazapDbContext context, ILogger logger, int farmaciaId)
    {
        await EnsureDepartamentosAsync(context, logger, farmaciaId);
        await EnsureEtiquetasAsync(context, logger, farmaciaId);
        await EnsureMensagensPreDefinidasAsync(context, logger, farmaciaId);
        await EnsureMotivosEncerramentoAsync(context, logger, farmaciaId);
    }

    private static async Task EnsureDepartamentosAsync(PharmazapDbContext context, ILogger logger, int farmaciaId)
    {
        var departamento = await context.Departamentos
            .FirstOrDefaultAsync(d => d.FarmaciaId == farmaciaId && d.Nome == DepartamentoPadraoNome);

        if (departamento == null)
        {
            departamento = new Departamento
            {
                FarmaciaId = farmaciaId,
                Nome = DepartamentoPadraoNome,
                Ativo = true,
                Farmacia = null
            };

            context.Departamentos.Add(departamento);
            await context.SaveChangesAsync();
            logger.LogInformation("Departamento padrão '{Nome}' criado para farmácia {FarmaciaId}.", DepartamentoPadraoNome, farmaciaId);
        }

    }

    private static async Task EnsureEtiquetasAsync(PharmazapDbContext context, ILogger logger, int farmaciaId)
    {
        foreach (var seed in SeedEtiquetas)
        {
            var existe = await context.Etiquetas
                .AnyAsync(e => e.FarmaciaId == farmaciaId && e.Nome == seed.Nome);

            if (!existe)
            {
                context.Etiquetas.Add(new Etiqueta
                {
                    FarmaciaId = farmaciaId,
                    Nome = seed.Nome,
                    Cor = seed.Cor,
                    Ativo = true,
                    Farmacia = null
                });
                logger.LogInformation("Etiqueta padrão '{Nome}' criada para farmácia {FarmaciaId}.", seed.Nome, farmaciaId);
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task EnsureMensagensPreDefinidasAsync(PharmazapDbContext context, ILogger logger, int farmaciaId)
    {
        foreach (var seed in SeedMensagensPreDefinidas)
        {
            var existe = await context.MensagensPreDefinidas
                .AnyAsync(m => m.FarmaciaId == farmaciaId && m.Titulo == seed.Titulo);

            if (!existe)
            {
                context.MensagensPreDefinidas.Add(new MensagemPreDefinida
                {
                    FarmaciaId = farmaciaId,
                    Titulo = seed.Titulo,
                    Texto = seed.Texto,
                    Ordem = seed.Ordem,
                    Ativo = true
                });

                logger.LogInformation("Mensagem pré-definida padrão '{Titulo}' criada para farmácia {FarmaciaId}.", seed.Titulo, farmaciaId);
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task EnsureMotivosEncerramentoAsync(PharmazapDbContext context, ILogger logger, int farmaciaId)
    {
        var seedMotivos = new[]
        {
            new { Nome = "Venda confirmada",                        ExigeValorVenda = true,  ExigeComprovante = true,  Ordem = 1  },
            new { Nome = "Venda confirmada (sem comprovante)",      ExigeValorVenda = true,  ExigeComprovante = false, Ordem = 2  },
            new { Nome = "Cliente ficou satisfeito",                ExigeValorVenda = false, ExigeComprovante = false, Ordem = 3  },
            new { Nome = "Cliente não comprou - medicamento caro",  ExigeValorVenda = false, ExigeComprovante = false, Ordem = 4  },
            new { Nome = "Cliente não comprou - indisponível",      ExigeValorVenda = false, ExigeComprovante = false, Ordem = 5  },
            new { Nome = "Cliente desistiu",                        ExigeValorVenda = false, ExigeComprovante = false, Ordem = 6  },
            new { Nome = "Cliente não respondeu",                   ExigeValorVenda = false, ExigeComprovante = false, Ordem = 7  },
            new { Nome = "Atendimento transferido",                 ExigeValorVenda = false, ExigeComprovante = false, Ordem = 8  },
            new { Nome = "Outro",                                   ExigeValorVenda = false, ExigeComprovante = false, Ordem = 99 },
        };

        foreach (var seed in seedMotivos)
        {
            var existe = await context.MotivosEncerramento
                .AnyAsync(m => m.FarmaciaId == farmaciaId && m.Nome == seed.Nome);

            if (!existe)
            {
                context.MotivosEncerramento.Add(new MotivoEncerramento
                {
                    FarmaciaId = farmaciaId,
                    Nome = seed.Nome,
                    ExigeValorVenda = seed.ExigeValorVenda,
                    ExigeComprovante = seed.ExigeComprovante,
                    Ativo = true,
                    Ordem = seed.Ordem,
                    Farmacia = null
                });
                logger.LogInformation("Motivo de encerramento padrão '{Nome}' criado para farmácia {FarmaciaId}.", seed.Nome, farmaciaId);
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task EnsureUserAsync(
        PharmazapDbContext context,
        ILogger logger,
        int farmaciaId,
        ICryptoService cryptoService,
        SeedUser seed,
        IConfiguration? config = null)
    {
        var exists = await context.UsuariosFarmacia
            .AnyAsync(u => u.Email == seed.Email && u.FarmaciaId == farmaciaId);

        if (exists)
        {
            logger.LogInformation("Usuário {Email} já existe, pulando.", seed.Email);
            return;
        }

        var password = config?[seed.PasswordConfigKey];
        if (string.IsNullOrWhiteSpace(password))
        {
            password = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            logger.LogWarning(
                "Senha de seed não configurada para {Email}. Uma senha aleatória foi gerada; "
                + "defina {ConfigKey} para controlar o acesso.",
                seed.Email,
                seed.PasswordConfigKey);
        }

        var (hash, salt) = GeneratePasswordHash(password);

        var user = new UsuarioFarmacia
        {
            Nome = seed.Nome,
            Email = seed.Email,
            Telefone = seed.Telefone,
            CPFEncrypted = cryptoService.Encrypt(seed.Cpf),
            Role = seed.Role,
            PasswordHash = hash,
            PasswordSalt = salt,
            Ativo = true,
            EmailConfirmado = true,
            FarmaciaId = farmaciaId,
            DataCadastro = DateTime.UtcNow,
            UltimoLogin = DateTime.UtcNow,
            Turno = seed.Turno,
            RecebeNotificacoes = true,
            TotalAtendimentos = 0,
            AtendimentosHoje = 0,
            Farmacia = null!
        };

        await context.UsuariosFarmacia.AddAsync(user);
        await context.SaveChangesAsync();

        logger.LogInformation("Usuário {Email} ({Role}) criado com sucesso.", seed.Email, seed.Role);
    }

    private static (string hash, string salt) GeneratePasswordHash(string password)
    {
        byte[] saltBytes = new byte[Consts.Database.PasswordSaltBytes];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(saltBytes);
        }
        string salt = Convert.ToBase64String(saltBytes);

        using var sha256 = SHA256.Create();
        var passwordBytes = Encoding.UTF8.GetBytes(password + salt);
        var hashBytes = sha256.ComputeHash(passwordBytes);
        string hash = Convert.ToBase64String(hashBytes);

        return (hash, salt);
    }

    private sealed record SeedUser(
        string Nome,
        string Email,
        string Telefone,
        string Cpf,
        string Role,
        string PasswordConfigKey,
        string Turno);
}
