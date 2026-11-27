#nullable enable
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using PharmazapPlus.API.Controllers;
using PharmazapPlus.API.Data;
using PharmazapPlus.API.Services.CRM;
using PharmazapPlus.API.Services.Queue;
using PharmazapPlus.API.Services.WhatsApp;
using PharmazapPlus.Shared;
using PharmazapPlus.Shared.Enums;
using PharmazapPlus.Shared.Models.Core;
using PharmazapPlus.Shared.Models.CRM;
using PharmazapPlus.Shared.Models.Mensagens;
using FluentAssertions;
using WhatsAppWebhookResult = PharmazapPlus.Shared.Models.Mensagens.WebhookProcessingResult;

namespace PharmazapPlus.API.Tests.Controllers;

public class WhatsAppControllerTests
{
    private const string TestPhoneNumber = "5562998808389";
    private const string TestPhoneNumberNormalized = "6298808389";
    private readonly Mock<IWhatsAppService> _mockWhatsAppService;
    private readonly Mock<ILogger<WhatsAppController>> _mockLogger;
    private readonly Mock<IMessageQueue> _mockMessageQueue;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ICrmService> _mockCrmService;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<IEvolutionGoCryptoService> _mockCryptoService;
    private readonly Mock<IRemetenteBloqueadoService> _mockRemetenteBloqueado;
    private readonly PharmazapDbContext _dbContext;
    private readonly WhatsAppController _controller;

    public WhatsAppControllerTests()
    {
        _mockWhatsAppService = new Mock<IWhatsAppService>();
        _mockLogger = new Mock<ILogger<WhatsAppController>>();
        _mockMessageQueue = new Mock<IMessageQueue>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockCrmService = new Mock<ICrmService>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockCryptoService = new Mock<IEvolutionGoCryptoService>();
        _mockRemetenteBloqueado = new Mock<IRemetenteBloqueadoService>();

        var dbOptions = new DbContextOptionsBuilder<PharmazapDbContext>()
            .UseInMemoryDatabase(databaseName: $"WhatsAppControllerTests_{Guid.NewGuid()}")
            .Options;
        _dbContext = new PharmazapDbContext(dbOptions);

        _controller = new WhatsAppController(
            _mockWhatsAppService.Object,
            _mockLogger.Object,
            _dbContext,
            _mockMessageQueue.Object,
            _mockConfiguration.Object,
            _mockCrmService.Object,
            _mockHttpClientFactory.Object,
            _mockCryptoService.Object,
            _mockRemetenteBloqueado.Object);
    }

    [Fact]
    public async Task VerifyWebhook_WithValidToken_ReturnsChallenge()
    {
        // Arrange
        var hubMode = "subscribe";
        var hubChallenge = "challenge_string";
        var hubVerifyToken = "valid_token";

        _mockWhatsAppService
            .Setup(s => s.ValidateWebhook(hubMode, hubVerifyToken, hubChallenge))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.VerifyWebhook(hubMode, hubChallenge, hubVerifyToken);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().Be(hubChallenge);
    }

    [Fact]
    public async Task VerifyWebhook_WithInvalidToken_ReturnsUnauthorized()
    {
        // Arrange
        var hubMode = "subscribe";
        var hubChallenge = "challenge_string";
        var hubVerifyToken = "invalid_token";

        _mockWhatsAppService
            .Setup(s => s.ValidateWebhook(hubMode, hubVerifyToken, hubChallenge))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.VerifyWebhook(hubMode, hubChallenge, hubVerifyToken);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task ProcessWebhook_WithValidPayload_ReturnsSuccess()
    {
        // Arrange
        var payload = new Dictionary<string, object>
        {
            { "entry", new[] { new { } } }
        };

        var processingResult = new WhatsAppWebhookResult
        {
            Success = true,
            Message = "Processed successfully",
            ProcessedMessage = null!,
            Atendimento = null!
        };

        _mockWhatsAppService
            .Setup(s => s.ProcessWebhookPayload(It.IsAny<Dictionary<string, object>>()))
            .ReturnsAsync(processingResult);

        // Act
        var result = await _controller.ProcessWebhook(payload);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task ProcessWebhook_WithInvalidPayload_ReturnsBadRequest()
    {
        // Arrange
        var payload = new Dictionary<string, object>();

        var processingResult = new WhatsAppWebhookResult
        {
            Success = false,
            Message = "Invalid payload",
            ProcessedMessage = null!,
            Atendimento = null!
        };

        _mockWhatsAppService
            .Setup(s => s.ProcessWebhookPayload(It.IsAny<Dictionary<string, object>>()))
            .ReturnsAsync(processingResult);

        // Act
        var result = await _controller.ProcessWebhook(payload);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ProcessWebhook_WhenExceptionThrown_ReturnsInternalServerError()
    {
        // Arrange
        var payload = new Dictionary<string, object>();

        _mockWhatsAppService
            .Setup(s => s.ProcessWebhookPayload(It.IsAny<Dictionary<string, object>>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.ProcessWebhook(payload);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = result as ObjectResult;
        objectResult!.StatusCode.Should().Be(500);
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        var controller = new WhatsAppController(
            _mockWhatsAppService.Object,
            _mockLogger.Object,
            _dbContext,
            _mockMessageQueue.Object,
            _mockConfiguration.Object,
            _mockCrmService.Object,
            _mockHttpClientFactory.Object,
            _mockCryptoService.Object,
            _mockRemetenteBloqueado.Object);

        // Assert
        controller.Should().NotBeNull();
    }

    [Fact]
    public async Task SimularMensagemCliente_WithDevelopmentModeEnabled_CreatesPendingAtendimento()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [Consts.WhatsAppConfig.WhatsAppDevelopmentMode] = "true"
            })
            .Build();

        var mockCrmService = new Mock<ICrmService>();
        var fakeCliente = new Cliente
        {
            Id = 1,
            FarmaciaId = 1,
            Farmacia = null!,
            WhatsAppId = TestPhoneNumber,
            Nome = TestPhoneNumber,
            CodigoCliente = "CLI-0001",
            Status = ClienteStatus.Potencial,
            PerfilCompra = ClientePerfilCompraEnum.Prospecto
        };
        mockCrmService
            .Setup(s => s.GetOrCreateClienteByWhatsApp("1", TestPhoneNumberNormalized, null))
            .ReturnsAsync(fakeCliente);

        var controller = new WhatsAppController(
            _mockWhatsAppService.Object,
            _mockLogger.Object,
            _dbContext,
            _mockMessageQueue.Object,
            configuration,
            mockCrmService.Object,
            _mockHttpClientFactory.Object,
            _mockCryptoService.Object,
            _mockRemetenteBloqueado.Object);

        _dbContext.Farmacias.Add(new Farmacia
        {
            Id = 1,
            Nome = "Farmácia Teste",
            Cnpj = "00000000000100",
            Telefone = "(62) 3333-4444",
            Email = "contato@farmaciateste.com.br",
            Endereco = "Rua das Flores, 123",
            Cidade = "Goiânia",
            Estado = "GO",
            Cep = "74000-000",
            Integracoes = new FarmaciaIntegracoes
            {
                WhatsAppPhoneNumberId = "mock-phone-id",
                WhatsAppAccessToken = "mock-access-token",
                WhatsAppBusinessAccountId = "mock-business-id",
                ChaveWhatsApp = "farmacia-teste-key"
            },
        });
        await _dbContext.SaveChangesAsync();

        var request = new SimularMensagemRequest
        {
            FarmaciaId = 1,
            From = TestPhoneNumber,
            Text = "Olá, preciso renovar minha receita de metformina"
        };

        // Act
        var result = await controller.SimularMensagemCliente(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value!;
        var success = (bool?)value.GetType().GetProperty("success")?.GetValue(value);
        success.Should().BeTrue();

        var clienteId = (int?)value.GetType().GetProperty("clienteId")?.GetValue(value);
        clienteId.Should().Be(fakeCliente.Id);

        var atendimento = await _dbContext.Atendimentos.SingleAsync();
        atendimento.Id.Should().BeGreaterThan(0);
        atendimento.ClienteId.Should().Be(fakeCliente.Id);
        atendimento.Status.Should().Be(AtendimentoStatusEnum.Pendente.ToString());
        atendimento.ComentarioAvaliacao.Should().Be(string.Empty);

        var mensagem = await _dbContext.Mensagens.SingleAsync();
        mensagem.AtendimentoId.Should().Be(atendimento.Id);

        mockCrmService.Verify(s => s.GetOrCreateClienteByWhatsApp("1", TestPhoneNumberNormalized, null), Times.Once);
    }

    [Fact]
    public async Task SimularMensagemCliente_WithGroupFlag_CreatesPendingGroupAtendimentoWithoutCliente()
    {
        var request = new SimularMensagemRequest
        {
            FarmaciaId = 1,
            From = "120363044444@g.us",
            Text = "Mensagem do grupo",
            IsGroup = true
        };

        var result = await _controller.SimularMensagemCliente(request);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value!;
        var success = (bool?)value.GetType().GetProperty("success")?.GetValue(value);
        success.Should().BeTrue();

        var atendimento = await _dbContext.Atendimentos.SingleAsync();
        atendimento.StatusEnum.Should().Be(AtendimentoStatusEnum.Pendente);
        atendimento.WhatsAppId.Should().Be("120363044444@g.us");
        atendimento.IsGrupo.Should().BeTrue();
        atendimento.ClienteId.Should().BeNull();
        (await _dbContext.Clientes.CountAsync()).Should().Be(0);

        var mensagem = await _dbContext.Mensagens.SingleAsync();
        mensagem.AtendimentoId.Should().Be(atendimento.Id);
        mensagem.Direction.Should().Be(MessageDirection.Inbound);
        mensagem.Text.Should().Be("Mensagem do grupo");

        _mockCrmService.Verify(
            s => s.GetOrCreateClienteByWhatsApp(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessWebhook_WithEvolutionGoPayload_CreatesPendingAtendimentoAndInboundMessage()
    {
        var farmacia = new Farmacia
        {
            Nome = "Farmácia Evolution",
            Cnpj = "11111111000199",
            Telefone = "(11) 3333-4444",
            Email = "evolution@farmacia.com",
            Endereco = "Rua A, 123",
            Cidade = "São Paulo",
            Estado = "SP",
            Cep = "01000-000",
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "CHAVE12345678"
            }
        };
        _dbContext.Farmacias.Add(farmacia);
        await _dbContext.SaveChangesAsync();

        var cliente = new Cliente
        {
            Id = 10,
            FarmaciaId = farmacia.Id,
            Farmacia = null!,
            WhatsAppId = "5511999999999",
            Nome = "Cliente Evolution",
            CodigoCliente = "CLI-0010",
            Status = ClienteStatus.Potencial,
            PerfilCompra = ClientePerfilCompraEnum.Prospecto
        };

        _mockCrmService
            .Setup(s => s.GetOrCreateClienteByWhatsApp(farmacia.Id.ToString(), "1199999999", null))
            .ReturnsAsync(cliente);

        var payload = new Dictionary<string, object>
        {
            ["event"] = "MESSAGES_UPSERT",
            ["instanceName"] = "CHAVE12345678",
            ["data"] = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                """
                {
                  "key": {
                    "remoteJid": "5511999999999@s.whatsapp.net",
                    "fromMe": false,
                    "id": "3EB0ABC"
                  },
                  "message": {
                    "conversation": "Olá!"
                  }
                }
                """)
        };

        var result = await _controller.ProcessWebhook(payload);

        result.Should().BeOfType<OkObjectResult>();
        var atendimento = await _dbContext.Atendimentos.SingleAsync();
        atendimento.StatusEnum.Should().Be(AtendimentoStatusEnum.Pendente);
        atendimento.WhatsAppId.Should().Be("1199999999");
        atendimento.ClienteId.Should().Be(cliente.Id);

        var mensagem = await _dbContext.Mensagens.SingleAsync();
        mensagem.AtendimentoId.Should().Be(atendimento.Id);
        mensagem.Direction.Should().Be(MessageDirection.Inbound);
        mensagem.Status.Should().Be(MessageStatus.Recebida);
        mensagem.Type.Should().Be(MessageType.Text);
        mensagem.Text.Should().Be("Olá!");

        _mockWhatsAppService.Verify(s => s.ProcessWebhookPayload(It.IsAny<Dictionary<string, object>>()), Times.Never);
    }

    [Fact]
    public async Task ProcessWebhook_WithEvolutionGoPayload_UsesContactNameWhenAvailable()
    {
        var farmacia = new Farmacia
        {
            Nome = "Farmácia Evolution",
            Cnpj = "11111111000199",
            Telefone = "(11) 3333-4444",
            Email = "evolution@farmacia.com",
            Endereco = "Rua A, 123",
            Cidade = "São Paulo",
            Estado = "SP",
            Cep = "01000-000",
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "CHAVE12345678"
            }
        };
        _dbContext.Farmacias.Add(farmacia);
        await _dbContext.SaveChangesAsync();

        var cliente = new Cliente
        {
            Id = 101,
            FarmaciaId = farmacia.Id,
            Farmacia = null!,
            WhatsAppId = "1199999999",
            Nome = "Maria Silva",
            Telefone = "1199999999",
            CodigoCliente = "CLI-0101",
            Status = ClienteStatus.Potencial,
            PerfilCompra = ClientePerfilCompraEnum.Prospecto
        };

        _mockCrmService
            .Setup(s => s.GetOrCreateClienteByWhatsApp(farmacia.Id.ToString(), "1199999999", "Maria Silva"))
            .ReturnsAsync(cliente);

        var payload = new Dictionary<string, object>
        {
            ["event"] = "MESSAGES_UPSERT",
            ["instanceName"] = "CHAVE12345678",
            ["data"] = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                """
                {
                  "key": {
                    "remoteJid": "5511999999999@s.whatsapp.net",
                    "fromMe": false,
                    "id": "3EB0NAME1"
                  },
                  "pushName": "Maria Silva",
                  "message": {
                    "conversation": "Olá!"
                  }
                }
                """)
        };

        var result = await _controller.ProcessWebhook(payload);

        result.Should().BeOfType<OkObjectResult>();
        _mockCrmService.Verify(
            s => s.GetOrCreateClienteByWhatsApp(farmacia.Id.ToString(), "1199999999", "Maria Silva"),
            Times.Once);
    }

    [Fact]
    public async Task ProcessWebhook_WithEvolutionGoGroupPayload_CreatesPendingGroupAtendimento()
    {
        var farmacia = new Farmacia
        {
            Nome = "Farmácia Evolution",
            Cnpj = "11111111000199",
            Telefone = "(11) 3333-4444",
            Email = "evolution@farmacia.com",
            Endereco = "Rua A, 123",
            Cidade = "São Paulo",
            Estado = "SP",
            Cep = "01000-000",
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "CHAVE12345678"
            }
        };
        _dbContext.Farmacias.Add(farmacia);
        await _dbContext.SaveChangesAsync();

        var payload = new Dictionary<string, object>
        {
            ["event"] = "MESSAGES_UPSERT",
            ["instanceName"] = "CHAVE12345678",
            ["data"] = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                """
                {
                  "key": {
                    "remoteJid": "120363044444@g.us",
                    "fromMe": false,
                    "id": "3EB0GROUP"
                  },
                  "message": {
                    "conversation": "Olá do grupo!"
                  }
                }
                """)
        };

        var result = await _controller.ProcessWebhook(payload);

        result.Should().BeOfType<OkObjectResult>();
        var atendimento = await _dbContext.Atendimentos.SingleAsync();
        atendimento.StatusEnum.Should().Be(AtendimentoStatusEnum.Pendente);
        atendimento.WhatsAppId.Should().Be("120363044444@g.us");
        atendimento.IsGrupo.Should().BeTrue();
        atendimento.ClienteId.Should().BeNull();
        (await _dbContext.Clientes.CountAsync()).Should().Be(0);

        var mensagem = await _dbContext.Mensagens.SingleAsync();
        mensagem.AtendimentoId.Should().Be(atendimento.Id);
        mensagem.Direction.Should().Be(MessageDirection.Inbound);
        mensagem.Text.Should().Be("Olá do grupo!");

        _mockCrmService.Verify(
            s => s.GetOrCreateClienteByWhatsApp(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()),
            Times.Never);
        _mockWhatsAppService.Verify(s => s.ProcessWebhookPayload(It.IsAny<Dictionary<string, object>>()), Times.Never);
    }

    [Fact]
    public async Task ProcessWebhook_WithEvolutionGoNewsletterPayload_IgnoresChannelMessage()
    {
        var farmacia = new Farmacia
        {
            Nome = "Farmácia Evolution",
            Cnpj = "11111111000199",
            Telefone = "(11) 3333-4444",
            Email = "evolution@farmacia.com",
            Endereco = "Rua A, 123",
            Cidade = "São Paulo",
            Estado = "SP",
            Cep = "01000-000",
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "CHAVE12345678"
            }
        };
        _dbContext.Farmacias.Add(farmacia);
        await _dbContext.SaveChangesAsync();

        var payload = new Dictionary<string, object>
        {
            ["event"] = "MESSAGES_UPSERT",
            ["instanceName"] = "CHAVE12345678",
            ["data"] = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                """
                {
                  "key": {
                    "remoteJid": "120363401648797715@newsletter",
                    "fromMe": false,
                    "id": "3AFAF3B1ED7E4A027CA5"
                  },
                  "message": {
                    "imageMessage": {
                      "caption": "Canal"
                    }
                  }
                }
                """)
        };

        var result = await _controller.ProcessWebhook(payload);

        result.Should().BeOfType<OkObjectResult>();
        (await _dbContext.Atendimentos.CountAsync()).Should().Be(0);
        (await _dbContext.Mensagens.CountAsync()).Should().Be(0);
        (await _dbContext.Clientes.CountAsync()).Should().Be(0);

        _mockCrmService.Verify(
            s => s.GetOrCreateClienteByWhatsApp(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessWebhook_WithEvolutionGoRawNewsletterSender_IgnoresChannelMessage()
    {
        var farmacia = new Farmacia
        {
            Nome = "Farmácia Evolution",
            Cnpj = "11111111000199",
            Telefone = "(11) 3333-4444",
            Email = "evolution@farmacia.com",
            Endereco = "Rua A, 123",
            Cidade = "São Paulo",
            Estado = "SP",
            Cep = "01000-000",
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "CHAVE12345678"
            }
        };
        _dbContext.Farmacias.Add(farmacia);
        await _dbContext.SaveChangesAsync();

        var payload = new Dictionary<string, object>
        {
            ["event"] = "MESSAGES_UPSERT",
            ["instanceName"] = "CHAVE12345678",
            ["data"] = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                """
                {
                  "Info": {
                    "Sender": "120363401648797715",
                    "Chat": "120363401648797715",
                    "IsFromMe": false,
                    "ID": "3AFAF3B1ED7E4A027CA6"
                  },
                  "Message": {
                    "conversation": "Mensagem de canal"
                  }
                }
                """)
        };

        var result = await _controller.ProcessWebhook(payload);

        result.Should().BeOfType<OkObjectResult>();
        (await _dbContext.Atendimentos.CountAsync()).Should().Be(0);
        (await _dbContext.Mensagens.CountAsync()).Should().Be(0);
        (await _dbContext.Clientes.CountAsync()).Should().Be(0);

        _mockCrmService.Verify(
            s => s.GetOrCreateClienteByWhatsApp(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessWebhook_WithEvolutionGoPayload_ReusesPendingAtendimentoByCliente()
    {
        var farmacia = new Farmacia
        {
            Nome = "Farmácia Evolution",
            Cnpj = "11111111000199",
            Telefone = "(11) 3333-4444",
            Email = "evolution@farmacia.com",
            Endereco = "Rua A, 123",
            Cidade = "São Paulo",
            Estado = "SP",
            Cep = "01000-000",
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "CHAVE12345678"
            }
        };
        _dbContext.Farmacias.Add(farmacia);
        await _dbContext.SaveChangesAsync();

        var cliente = new Cliente
        {
            Id = 20,
            FarmaciaId = farmacia.Id,
            Farmacia = null!,
            WhatsAppId = "6195178597",
            Nome = "Cliente Evolution",
            CodigoCliente = "CLI-0020",
            Status = ClienteStatus.Potencial,
            PerfilCompra = ClientePerfilCompraEnum.Prospecto
        };

        var atendimentoExistente = new Atendimento
        {
            FarmaciaId = farmacia.Id,
            Farmacia = null!,
            ClienteId = cliente.Id,
            Cliente = null!,
            Atendente = null!,
            WhatsAppId = "556195178597",
            CanalEnum = CanalEnum.WhatsApp,
            StatusEnum = AtendimentoStatusEnum.Pendente,
            DataInicio = DateTime.UtcNow.AddMinutes(-5)
        };
        _dbContext.Atendimentos.Add(atendimentoExistente);
        await _dbContext.SaveChangesAsync();

        _mockCrmService
            .Setup(s => s.GetOrCreateClienteByWhatsApp(farmacia.Id.ToString(), "6195178597", null))
            .ReturnsAsync(cliente);

        var payload = new Dictionary<string, object>
        {
            ["event"] = "MESSAGES_UPSERT",
            ["instanceName"] = "CHAVE12345678",
            ["data"] = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                """
                {
                  "key": {
                    "remoteJid": "556195178597@s.whatsapp.net",
                    "fromMe": false,
                    "id": "3EB0REUSE1"
                  },
                  "message": {
                    "conversation": "Olá novamente!"
                  }
                }
                """)
        };

        var result = await _controller.ProcessWebhook(payload);

        result.Should().BeOfType<OkObjectResult>();
        (await _dbContext.Atendimentos.CountAsync()).Should().Be(1);

        var atendimentoAtualizado = await _dbContext.Atendimentos.SingleAsync();
        atendimentoAtualizado.Id.Should().Be(atendimentoExistente.Id);

        var mensagem = await _dbContext.Mensagens.SingleAsync();
        mensagem.AtendimentoId.Should().Be(atendimentoExistente.Id);
    }

    [Fact]
    public async Task ProcessWebhook_WithEvolutionGoPayload_ReusesEmAndamentoAtendimentoByCliente()
    {
        var farmacia = new Farmacia
        {
            Nome = "Farmácia Evolution",
            Cnpj = "11111111000199",
            Telefone = "(11) 3333-4444",
            Email = "evolution@farmacia.com",
            Endereco = "Rua A, 123",
            Cidade = "São Paulo",
            Estado = "SP",
            Cep = "01000-000",
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "CHAVE12345678"
            }
        };
        _dbContext.Farmacias.Add(farmacia);
        await _dbContext.SaveChangesAsync();

        var cliente = new Cliente
        {
            Id = 21,
            FarmaciaId = farmacia.Id,
            Farmacia = null!,
            WhatsAppId = "1199999999",
            Nome = "Cliente Evolution",
            CodigoCliente = "CLI-0021",
            Status = ClienteStatus.Potencial,
            PerfilCompra = ClientePerfilCompraEnum.Prospecto
        };

        var atendimentoExistente = new Atendimento
        {
            FarmaciaId = farmacia.Id,
            Farmacia = null!,
            ClienteId = cliente.Id,
            Cliente = null!,
            Atendente = null!,
            WhatsAppId = "5511999999999",
            CanalEnum = CanalEnum.WhatsApp,
            StatusEnum = AtendimentoStatusEnum.EmAndamento,
            DataInicio = DateTime.UtcNow.AddMinutes(-5)
        };
        _dbContext.Atendimentos.Add(atendimentoExistente);
        await _dbContext.SaveChangesAsync();

        _mockCrmService
            .Setup(s => s.GetOrCreateClienteByWhatsApp(farmacia.Id.ToString(), "1199999999", null))
            .ReturnsAsync(cliente);

        var payload = new Dictionary<string, object>
        {
            ["event"] = "MESSAGES_UPSERT",
            ["instanceName"] = "CHAVE12345678",
            ["data"] = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                """
                {
                  "key": {
                    "remoteJid": "5511999999999@s.whatsapp.net",
                    "fromMe": false,
                    "id": "3EB0REUSE2"
                  },
                  "message": {
                    "conversation": "Mensagem em atendimento ativo"
                  }
                }
                """)
        };

        var result = await _controller.ProcessWebhook(payload);

        result.Should().BeOfType<OkObjectResult>();
        (await _dbContext.Atendimentos.CountAsync()).Should().Be(1);

        var atendimentoAtualizado = await _dbContext.Atendimentos.SingleAsync();
        atendimentoAtualizado.Id.Should().Be(atendimentoExistente.Id);
        atendimentoAtualizado.StatusEnum.Should().Be(AtendimentoStatusEnum.EmAndamento);

        var mensagem = await _dbContext.Mensagens.SingleAsync();
        mensagem.AtendimentoId.Should().Be(atendimentoExistente.Id);
    }

    [Fact]
    public async Task SimularMensagemCliente_WithEquivalentPhone_ReusesPendingAtendimentoByCliente()
    {
        var cliente = new Cliente
        {
            Id = 30,
            FarmaciaId = 1,
            Farmacia = null!,
            WhatsAppId = "11912345678",
            Nome = "Cliente Teste",
            Telefone = "11912345678",
            CodigoCliente = "CLI-0030",
            Status = ClienteStatus.Potencial,
            PerfilCompra = ClientePerfilCompraEnum.Prospecto
        };

        var atendimentoExistente = new Atendimento
        {
            FarmaciaId = 1,
            Farmacia = null!,
            ClienteId = cliente.Id,
            Cliente = null!,
            Atendente = null!,
            WhatsAppId = "11912345678",
            CanalEnum = CanalEnum.WhatsApp,
            StatusEnum = AtendimentoStatusEnum.Pendente,
            DataInicio = DateTime.UtcNow.AddMinutes(-10)
        };

        _dbContext.Atendimentos.Add(atendimentoExistente);
        await _dbContext.SaveChangesAsync();

        _mockCrmService
            .Setup(s => s.GetOrCreateClienteByWhatsApp(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(cliente);

        var request = new SimularMensagemRequest
        {
            FarmaciaId = 1,
            From = "5531912345678",
            Text = "Olá novamente"
        };

        var result = await _controller.SimularMensagemCliente(request);

        result.Should().BeOfType<OkObjectResult>();
        (await _dbContext.Atendimentos.CountAsync()).Should().Be(1);
        var mensagem = await _dbContext.Mensagens.SingleAsync();
        mensagem.AtendimentoId.Should().Be(atendimentoExistente.Id);
    }

    [Fact]
    public async Task SimularMensagemCliente_WithEquivalentPhone_ReusesEmAndamentoAtendimentoByCliente()
    {
        var cliente = new Cliente
        {
            Id = 31,
            FarmaciaId = 1,
            Farmacia = null!,
            WhatsAppId = "11912345678",
            Nome = "Cliente Teste",
            Telefone = "11912345678",
            CodigoCliente = "CLI-0031",
            Status = ClienteStatus.Potencial,
            PerfilCompra = ClientePerfilCompraEnum.Prospecto
        };

        var atendimentoExistente = new Atendimento
        {
            FarmaciaId = 1,
            Farmacia = null!,
            ClienteId = cliente.Id,
            Cliente = null!,
            Atendente = null!,
            WhatsAppId = "11912345678",
            CanalEnum = CanalEnum.WhatsApp,
            StatusEnum = AtendimentoStatusEnum.EmAndamento,
            DataInicio = DateTime.UtcNow.AddMinutes(-10)
        };

        _dbContext.Atendimentos.Add(atendimentoExistente);
        await _dbContext.SaveChangesAsync();

        _mockCrmService
            .Setup(s => s.GetOrCreateClienteByWhatsApp(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(cliente);

        var request = new SimularMensagemRequest
        {
            FarmaciaId = 1,
            From = "5531912345678",
            Text = "Mensagem em atendimento em andamento"
        };

        var result = await _controller.SimularMensagemCliente(request);

        result.Should().BeOfType<OkObjectResult>();
        (await _dbContext.Atendimentos.CountAsync()).Should().Be(1);
        var atendimentoAtualizado = await _dbContext.Atendimentos.SingleAsync();
        atendimentoAtualizado.Id.Should().Be(atendimentoExistente.Id);
        atendimentoAtualizado.StatusEnum.Should().Be(AtendimentoStatusEnum.EmAndamento);
        var mensagem = await _dbContext.Mensagens.SingleAsync();
        mensagem.AtendimentoId.Should().Be(atendimentoExistente.Id);
    }

    [Fact]
    public async Task ProcessWebhook_WithEvolutionGoImageMessage_PersistsMediaFields()
    {
        var farmacia = new Farmacia
        {
            Nome = "Farmácia Evolution",
            Cnpj = "11111111000199",
            Telefone = "(11) 3333-4444",
            Email = "evolution@farmacia.com",
            Endereco = "Rua A, 123",
            Cidade = "São Paulo",
            Estado = "SP",
            Cep = "01000-000",
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "CHAVE12345678"
            }
        };
        _dbContext.Farmacias.Add(farmacia);
        await _dbContext.SaveChangesAsync();

        var cliente = new Cliente
        {
            Id = 11,
            FarmaciaId = farmacia.Id,
            Farmacia = null!,
            WhatsAppId = "5511999999999",
            Nome = "Cliente Evolution",
            CodigoCliente = "CLI-0011",
            Status = ClienteStatus.Potencial,
            PerfilCompra = ClientePerfilCompraEnum.Prospecto
        };

        _mockCrmService
            .Setup(s => s.GetOrCreateClienteByWhatsApp(farmacia.Id.ToString(), "1199999999", null))
            .ReturnsAsync(cliente);

        var payload = new Dictionary<string, object>
        {
            ["event"] = "MESSAGES_UPSERT",
            ["instanceName"] = "CHAVE12345678",
            ["data"] = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                """
                {
                  "key": {
                    "remoteJid": "5511999999999@s.whatsapp.net",
                    "fromMe": false,
                    "id": "3EB0MEDIA1"
                  },
                  "message": {
                    "imageMessage": {
                      "url": "https://media.exemplo.com/img-1.jpg",
                      "caption": "Receita anexada",
                      "fileLength": "1024"
                    }
                  }
                }
                """)
        };

        var result = await _controller.ProcessWebhook(payload);

        result.Should().BeOfType<OkObjectResult>();
        var mensagem = await _dbContext.Mensagens.SingleAsync();
        mensagem.Type.Should().Be(MessageType.Image);
        mensagem.Text.Should().Be("Receita anexada");
        mensagem.MediaType.Should().Be("image");
        mensagem.MediaUrl.Should().Be("https://media.exemplo.com/img-1.jpg");
        mensagem.MediaCaption.Should().Be("Receita anexada");
        mensagem.MediaSize.Should().Be(1024);
    }

    [Fact]
    public async Task ProcessWebhook_WithEvolutionGoAudioMessage_PersistsMediaFields()
    {
        var farmacia = new Farmacia
        {
            Nome = "Farmácia Evolution",
            Cnpj = "11111111000199",
            Telefone = "(11) 3333-4444",
            Email = "evolution@farmacia.com",
            Endereco = "Rua A, 123",
            Cidade = "São Paulo",
            Estado = "SP",
            Cep = "01000-000",
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "CHAVE12345678"
            }
        };
        _dbContext.Farmacias.Add(farmacia);
        await _dbContext.SaveChangesAsync();

        var cliente = new Cliente
        {
            Id = 12,
            FarmaciaId = farmacia.Id,
            Farmacia = null!,
            WhatsAppId = "5511999999999",
            Nome = "Cliente Evolution",
            CodigoCliente = "CLI-0012",
            Status = ClienteStatus.Potencial,
            PerfilCompra = ClientePerfilCompraEnum.Prospecto
        };

        _mockCrmService
            .Setup(s => s.GetOrCreateClienteByWhatsApp(farmacia.Id.ToString(), "1199999999", null))
            .ReturnsAsync(cliente);

        var payload = new Dictionary<string, object>
        {
            ["event"] = "MESSAGES_UPSERT",
            ["instanceName"] = "CHAVE12345678",
            ["data"] = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                """
                {
                  "key": {
                    "remoteJid": "5511999999999@s.whatsapp.net",
                    "fromMe": false,
                    "id": "3EB0MEDIA2"
                  },
                  "Message": {
                    "audioMessage": {
                      "url": "https://mmg.whatsapp.net/audio.enc",
                      "downloadUrl": "https://media.exemplo.com/audio-1.ogg",
                      "mimetype": "audio/ogg; codecs=opus"
                    }
                  }
                }
                """)
        };

        var result = await _controller.ProcessWebhook(payload);

        result.Should().BeOfType<OkObjectResult>();
        var mensagem = await _dbContext.Mensagens.SingleAsync();
        mensagem.Type.Should().Be(MessageType.Audio);
        mensagem.Text.Should().Be("[Áudio]");
        mensagem.MediaType.Should().Be("audio/ogg; codecs=opus");
        mensagem.MediaUrl.Should().Be("https://media.exemplo.com/audio-1.ogg");
    }

    [Fact]
    public async Task ProcessWebhook_WithEvolutionGoPascalCaseVideoMessage_PersistsMediaFields()
    {
        var farmacia = new Farmacia
        {
            Nome = "Farmácia Evolution",
            Cnpj = "11111111000199",
            Telefone = "(11) 3333-4444",
            Email = "evolution@farmacia.com",
            Endereco = "Rua A, 123",
            Cidade = "São Paulo",
            Estado = "SP",
            Cep = "01000-000",
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "CHAVE12345678"
            }
        };
        _dbContext.Farmacias.Add(farmacia);
        await _dbContext.SaveChangesAsync();

        var cliente = new Cliente
        {
            Id = 13,
            FarmaciaId = farmacia.Id,
            Farmacia = null!,
            WhatsAppId = "5511999999999",
            Nome = "Cliente Evolution",
            CodigoCliente = "CLI-0013",
            Status = ClienteStatus.Potencial,
            PerfilCompra = ClientePerfilCompraEnum.Prospecto
        };

        _mockCrmService
            .Setup(s => s.GetOrCreateClienteByWhatsApp(farmacia.Id.ToString(), "1199999999", null))
            .ReturnsAsync(cliente);

        var payload = new Dictionary<string, object>
        {
            ["event"] = "MESSAGES_UPSERT",
            ["instanceName"] = "CHAVE12345678",
            ["data"] = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                """
                {
                  "Info": {
                    "Sender": "5511999999999@s.whatsapp.net",
                    "IsFromMe": false,
                    "ID": "3EB0MEDIA3"
                  },
                  "messageType": "videoMessage",
                  "Message": {
                    "VideoMessage": {
                      "URL": "https://media.exemplo.com/video-1.mp4",
                      "Caption": "Vídeo do produto",
                      "FileLength": 2048
                    }
                  }
                }
                """)
        };

        var result = await _controller.ProcessWebhook(payload);

        result.Should().BeOfType<OkObjectResult>();
        var mensagem = await _dbContext.Mensagens.SingleAsync();
        mensagem.Type.Should().Be(MessageType.Video);
        mensagem.Text.Should().Be("Vídeo do produto");
        mensagem.MediaType.Should().Be("video");
        mensagem.MediaUrl.Should().Be("https://media.exemplo.com/video-1.mp4");
        mensagem.MediaCaption.Should().Be("Vídeo do produto");
        mensagem.MediaSize.Should().Be(2048);
    }

    [Fact]
    public async Task ProcessWebhook_WithEvolutionGoDocumentMessage_PrefersDownloadUrlMimeTypeAndFileName()
    {
        var farmacia = new Farmacia
        {
            Nome = "Farmácia Evolution",
            Cnpj = "11111111000199",
            Telefone = "(11) 3333-4444",
            Email = "evolution@farmacia.com",
            Endereco = "Rua A, 123",
            Cidade = "São Paulo",
            Estado = "SP",
            Cep = "01000-000",
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "CHAVE12345678"
            }
        };
        _dbContext.Farmacias.Add(farmacia);
        await _dbContext.SaveChangesAsync();

        var cliente = new Cliente
        {
            Id = 14,
            FarmaciaId = farmacia.Id,
            Farmacia = null!,
            WhatsAppId = "5511999999999",
            Nome = "Cliente Evolution",
            CodigoCliente = "CLI-0014",
            Status = ClienteStatus.Potencial,
            PerfilCompra = ClientePerfilCompraEnum.Prospecto
        };

        _mockCrmService
            .Setup(s => s.GetOrCreateClienteByWhatsApp(farmacia.Id.ToString(), "1199999999", null))
            .ReturnsAsync(cliente);

        var payload = new Dictionary<string, object>
        {
            ["event"] = "MESSAGES_UPSERT",
            ["instanceName"] = "CHAVE12345678",
            ["data"] = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                """
                {
                  "key": {
                    "remoteJid": "5511999999999@s.whatsapp.net",
                    "fromMe": false,
                    "id": "3EB0MEDIA4"
                  },
                  "Message": {
                    "documentMessage": {
                      "url": "https://mmg.whatsapp.net/file.enc",
                      "downloadUrl": "https://media.exemplo.com/relatorio.xlsx",
                      "mimetype": "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                      "fileName": "relatorio.xlsx",
                      "fileLength": 4096
                    }
                  }
                }
                """)
        };

        var result = await _controller.ProcessWebhook(payload);

        result.Should().BeOfType<OkObjectResult>();
        var mensagem = await _dbContext.Mensagens.SingleAsync();
        mensagem.Type.Should().Be(MessageType.Document);
        mensagem.Text.Should().Be("relatorio.xlsx");
        mensagem.MediaType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        mensagem.MediaUrl.Should().Be("https://media.exemplo.com/relatorio.xlsx");
        mensagem.MediaCaption.Should().Be("relatorio.xlsx");
        mensagem.MediaSize.Should().Be(4096);
    }

    [Fact]
    public async Task ProcessWebhook_WithEvolutionGoMessageTypeAndDirectMediaPayload_PersistsMediaFields()
    {
        var farmacia = new Farmacia
        {
            Nome = "Farmácia Evolution",
            Cnpj = "11111111000199",
            Telefone = "(11) 3333-4444",
            Email = "evolution@farmacia.com",
            Endereco = "Rua A, 123",
            Cidade = "São Paulo",
            Estado = "SP",
            Cep = "01000-000",
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "CHAVE12345678"
            }
        };
        _dbContext.Farmacias.Add(farmacia);
        await _dbContext.SaveChangesAsync();

        var cliente = new Cliente
        {
            Id = 14,
            FarmaciaId = farmacia.Id,
            Farmacia = null!,
            WhatsAppId = "5511999999999",
            Nome = "Cliente Evolution",
            CodigoCliente = "CLI-0014",
            Status = ClienteStatus.Potencial,
            PerfilCompra = ClientePerfilCompraEnum.Prospecto
        };

        _mockCrmService
            .Setup(s => s.GetOrCreateClienteByWhatsApp(farmacia.Id.ToString(), "1199999999", null))
            .ReturnsAsync(cliente);

        var payload = new Dictionary<string, object>
        {
            ["event"] = "MESSAGES_UPSERT",
            ["instanceName"] = "CHAVE12345678",
            ["data"] = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                """
                {
                  "key": {
                    "remoteJid": "5511999999999@s.whatsapp.net",
                    "fromMe": false,
                    "id": "3EB0MEDIA4"
                  },
                  "messageType": "imageMessage",
                  "Message": {
                    "url": "https://media.exemplo.com/img-direta.jpg",
                    "caption": "Imagem direta",
                    "fileLength": "4096"
                  }
                }
                """)
        };

        var result = await _controller.ProcessWebhook(payload);

        result.Should().BeOfType<OkObjectResult>();
        var mensagem = await _dbContext.Mensagens.SingleAsync();
        mensagem.Type.Should().Be(MessageType.Image);
        mensagem.Text.Should().Be("Imagem direta");
        mensagem.MediaType.Should().Be("image");
        mensagem.MediaUrl.Should().Be("https://media.exemplo.com/img-direta.jpg");
        mensagem.MediaCaption.Should().Be("Imagem direta");
        mensagem.MediaSize.Should().Be(4096);
    }

    [Fact]
    public async Task ProcessWebhook_WithInlineBase64Media_SavesMediaWithoutExternalDownload()
    {
        var farmacia = new Farmacia
        {
            Nome = "Farmácia Base64",
            Cnpj = "11111111000155",
            Telefone = "(11) 3333-5555",
            Email = "base64@farmacia.com",
            Endereco = "Rua Base64, 123",
            Cidade = "São Paulo",
            Estado = "SP",
            Cep = "01000-000",
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "CHAVEBASE64"
            }
        };
        _dbContext.Farmacias.Add(farmacia);
        await _dbContext.SaveChangesAsync();

        var cliente = new Cliente
        {
            Id = 15,
            FarmaciaId = farmacia.Id,
            Farmacia = null!,
            WhatsAppId = "5511999999999",
            Nome = "Cliente Base64",
            CodigoCliente = "CLI-0015",
            Status = ClienteStatus.Potencial,
            PerfilCompra = ClientePerfilCompraEnum.Prospecto
        };

        _mockCrmService
            .Setup(s => s.GetOrCreateClienteByWhatsApp(farmacia.Id.ToString(), "1199999999", null))
            .ReturnsAsync(cliente);

        _mockHttpClientFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Throws(new InvalidOperationException("HttpClient should not be created when media is inline base64"));

        var payload = new Dictionary<string, object>
        {
            ["event"] = "MESSAGES_UPSERT",
            ["instanceName"] = "CHAVEBASE64",
            ["data"] = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                """
                {
                  "key": {
                    "remoteJid": "5511999999999@s.whatsapp.net",
                    "fromMe": false,
                    "id": "3EB0MEDIAINLINE01"
                  },
                  "messageType": "imageMessage",
                  "Message": {
                    "base64": "/9j/4A==",
                    "mimetype": "image/jpeg",
                    "caption": "Imagem inline"
                  }
                }
                """)
        };

        var result = await _controller.ProcessWebhook(payload);

        result.Should().BeOfType<OkObjectResult>();
        var mensagem = await _dbContext.Mensagens.SingleAsync();
        mensagem.Type.Should().Be(MessageType.Image);
        mensagem.Text.Should().Be("Imagem inline");
        mensagem.MediaType.Should().Be("image/jpeg");
        mensagem.MediaUrl.Should().StartWith(Consts.Midia.UploadUrlPrefix);
        mensagem.MediaUrl.Should().EndWith(".jpg");
    }

    [Fact]
    public async Task ProcessWebhook_WithInlineDataUrlInsideWrapper_SavesMediaWithoutExternalDownload()
    {
        var farmacia = new Farmacia
        {
            Nome = "Farmácia Data URL",
            Cnpj = "11111111000166",
            Telefone = "(11) 3333-6666",
            Email = "dataurl@farmacia.com",
            Endereco = "Rua Data URL, 456",
            Cidade = "São Paulo",
            Estado = "SP",
            Cep = "01000-000",
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "CHAVEDATAURL"
            }
        };
        _dbContext.Farmacias.Add(farmacia);
        await _dbContext.SaveChangesAsync();

        var cliente = new Cliente
        {
            Id = 16,
            FarmaciaId = farmacia.Id,
            Farmacia = null!,
            WhatsAppId = "5511999999999",
            Nome = "Cliente Data URL",
            CodigoCliente = "CLI-0016",
            Status = ClienteStatus.Potencial,
            PerfilCompra = ClientePerfilCompraEnum.Prospecto
        };

        _mockCrmService
            .Setup(s => s.GetOrCreateClienteByWhatsApp(farmacia.Id.ToString(), "1199999999", null))
            .ReturnsAsync(cliente);

        _mockHttpClientFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Throws(new InvalidOperationException("HttpClient should not be created when media is inline data URL"));

        var payload = new Dictionary<string, object>
        {
            ["event"] = "MESSAGES_UPSERT",
            ["instanceName"] = "CHAVEDATAURL",
            ["data"] = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                """
                {
                  "key": {
                    "remoteJid": "5511999999999@s.whatsapp.net",
                    "fromMe": false,
                    "id": "3EB0MEDIAINLINE02"
                  },
                  "message": {
                    "imageMessage": {
                      "data": {
                        "base64": "data:image/jpeg;base64,/9j/4A=="
                      },
                      "mimetype": "image/jpeg",
                      "caption": "Imagem data URL"
                    }
                  }
                }
                """)
        };

        var result = await _controller.ProcessWebhook(payload);

        result.Should().BeOfType<OkObjectResult>();
        var mensagem = await _dbContext.Mensagens.SingleAsync();
        mensagem.Type.Should().Be(MessageType.Image);
        mensagem.Text.Should().Be("Imagem data URL");
        mensagem.MediaType.Should().Be("image/jpeg");
        mensagem.MediaUrl.Should().StartWith(Consts.Midia.UploadUrlPrefix);
        mensagem.MediaUrl.Should().EndWith(".jpg");
    }

    [Fact]
    public async Task ProcessWebhook_WithEvolutionGoFromMe_DoesNotPersistInboundData()
    {
        var payload = new Dictionary<string, object>
        {
            ["event"] = "MESSAGES_UPSERT",
            ["instanceName"] = "CHAVE12345678",
            ["data"] = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                """
                {
                  "key": {
                    "remoteJid": "5511999999999@s.whatsapp.net",
                    "fromMe": true,
                    "id": "3EB0DEF"
                  },
                  "message": {
                    "conversation": "Mensagem enviada"
                  }
                }
                """)
        };

        var result = await _controller.ProcessWebhook(payload);

        result.Should().BeOfType<OkObjectResult>();
        (await _dbContext.Atendimentos.CountAsync()).Should().Be(0);
        (await _dbContext.Mensagens.CountAsync()).Should().Be(0);
        _mockWhatsAppService.Verify(s => s.ProcessWebhookPayload(It.IsAny<Dictionary<string, object>>()), Times.Never);
    }

    [Theory]
    [InlineData("https://mmg.whatsapp.net/v/t62.7119-24/image.enc", true)]
    [InlineData("https://mmg-files.whatsapp.net/audio.enc", true)]
    [InlineData("https://media.whatsapp.net/v/photo.jpg", true)]
    [InlineData("http://mmg.whatsapp.net/image.enc", false)]                   // HTTP not HTTPS
    [InlineData("https://media.exemplo.com/img.jpg", false)]                   // non-CDN host
    [InlineData("https://mmg-files.attacker.com/evil.enc", false)]             // subdomain bypass attempt
    [InlineData("https://169.254.169.254/latest/meta-data/", false)]           // metadata endpoint
    [InlineData("ftp://mmg.whatsapp.net/file.enc", false)]                     // wrong scheme
    [InlineData("", false)]                                                    // empty
    [InlineData("not-a-url", false)]                                           // invalid
    public void IsAllowedMediaUrl_VariousUrls_ReturnsExpected(string url, bool expected)
    {
        WhatsAppController.IsAllowedMediaUrl(url).Should().Be(expected);
    }

    [Fact]
    public async Task ProcessWebhook_WithWhatsAppCdnMediaUrl_StoresMediaLocallyAndUpdatesUrl()
    {
        var farmacia = new Farmacia
        {
            Nome = "Farmácia CDN Test",
            Cnpj = "22222222000188",
            Telefone = "(11) 4444-5555",
            Email = "cdntest@farmacia.com",
            Endereco = "Rua B, 456",
            Cidade = "Rio de Janeiro",
            Estado = "RJ",
            Cep = "20000-000",
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "CHAVECDNTEST"
            }
        };
        _dbContext.Farmacias.Add(farmacia);
        await _dbContext.SaveChangesAsync();

        var cliente = new Cliente
        {
            Id = 40,
            FarmaciaId = farmacia.Id,
            Farmacia = null!,
            WhatsAppId = "1199887766",
            Nome = "Cliente CDN",
            CodigoCliente = "CLI-0040",
            Status = ClienteStatus.Potencial,
            PerfilCompra = ClientePerfilCompraEnum.Prospecto
        };

        _mockCrmService
            .Setup(s => s.GetOrCreateClienteByWhatsApp(farmacia.Id.ToString(), "1199887766", null))
            .ReturnsAsync(cliente);

        // Simulate HTTP response from WhatsApp CDN
        var fakeHandler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }) // JPEG magic bytes
            {
                Headers = { { "Content-Type", "image/jpeg" } }
            }
        });
        _mockHttpClientFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(fakeHandler));

        var cdnUrl = "https://mmg.whatsapp.net/v/t62.7119-24/test-image.enc";
        var payload = new Dictionary<string, object>
        {
            ["event"] = "MESSAGES_UPSERT",
            ["instanceName"] = "CHAVECDNTEST",
            ["data"] = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                $$"""
                {
                  "key": {
                    "remoteJid": "551199887766@s.whatsapp.net",
                    "fromMe": false,
                    "id": "3EB0CDN01"
                  },
                  "message": {
                    "imageMessage": {
                      "url": "{{cdnUrl}}",
                      "caption": "Foto CDN",
                      "fileLength": "4"
                    }
                  }
                }
                """)
        };

        var result = await _controller.ProcessWebhook(payload);

        result.Should().BeOfType<OkObjectResult>();
        var mensagem = await _dbContext.Mensagens.SingleAsync();
        mensagem.MediaUrl.Should().NotBeNullOrWhiteSpace();
        mensagem.MediaUrl.Should().StartWith(Consts.Midia.UploadUrlPrefix);
        mensagem.MediaUrl.Should().NotBe(cdnUrl);
        mensagem.MediaUrl.Should().EndWith(".jpg");
    }

    [Fact]
    public async Task ProcessWebhook_WithMediaKeyAndDecryptFailure_DoesNotStoreEncryptedMediaLocally()
    {
        var farmacia = new Farmacia
        {
            Nome = "Farmácia Fallback Test",
            Cnpj = "22222222000177",
            Telefone = "(11) 4444-6666",
            Email = "fallback@farmacia.com",
            Endereco = "Rua Fallback, 10",
            Cidade = "São Paulo",
            Estado = "SP",
            Cep = "01000-000",
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "CHAVEFALLBACK"
            }
        };
        _dbContext.Farmacias.Add(farmacia);
        await _dbContext.SaveChangesAsync();

        var cliente = new Cliente
        {
            Id = 42,
            FarmaciaId = farmacia.Id,
            Farmacia = null!,
            WhatsAppId = "1199776655",
            Nome = "Cliente Fallback",
            CodigoCliente = "CLI-0042",
            Status = ClienteStatus.Potencial,
            PerfilCompra = ClientePerfilCompraEnum.Prospecto
        };

        _mockCrmService
            .Setup(s => s.GetOrCreateClienteByWhatsApp(farmacia.Id.ToString(), "1199776655", null))
            .ReturnsAsync(cliente);

        _mockCryptoService
            .Setup(s => s.DecryptMedia(It.IsAny<byte[]>(), It.IsAny<string?>(), It.IsAny<string>()))
            .Returns((byte[]?)null);

        var handler = new RoutingFakeHttpMessageHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (request.Method == HttpMethod.Get &&
                request.RequestUri?.Host.Contains("whatsapp.net", StringComparison.OrdinalIgnoreCase) == true)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(new byte[] { 0x00, 0x11, 0x22, 0x33 })
                    {
                        Headers = { { "Content-Type", "image/jpeg" } }
                    }
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{}")
            };
        });

        _mockHttpClientFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handler));

        var cdnUrl = "https://mmg.whatsapp.net/v/t62.7119-24/test-encrypted-image.enc";
        var payload = new Dictionary<string, object>
        {
            ["event"] = "MESSAGES_UPSERT",
            ["instanceName"] = "CHAVEFALLBACK",
            ["data"] = new
            {
                key = new
                {
                    remoteJid = "551199776655@s.whatsapp.net",
                    fromMe = false,
                    id = "3EB0CDN02"
                },
                messageType = "imageMessage",
                message = new
                {
                    imageMessage = new
                    {
                        url = cdnUrl,
                        mediaKey = "AQIDBA==",
                        mimetype = "image/jpeg",
                        caption = "Foto criptografada"
                    }
                }
            }
        };

        var result = await _controller.ProcessWebhook(payload);

        result.Should().BeOfType<OkObjectResult>();
        handler.RequestedUrls.Should().ContainSingle(url => url == "/v/t62.7119-24/test-encrypted-image.enc");
        var mensagem = await _dbContext.Mensagens.SingleAsync();
        mensagem.MediaUrl.Should().NotBeNullOrWhiteSpace();
        mensagem.MediaUrl.Should().Be(cdnUrl);
    }

    [Fact]
    public async Task ProcessWebhook_WithAudioMediaKeyAndDecryptFailure_DoesNotStoreEncryptedMediaLocally()
    {
        var farmacia = new Farmacia
        {
            Nome = "Farmácia Audio Test",
            Cnpj = "55555555000165",
            Telefone = "(11) 4000-1111",
            Email = "audio@farmacia.com",
            Endereco = "Rua Audio, 30",
            Cidade = "São Paulo",
            Estado = "SP",
            Cep = "01100-000",
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "CHAVEAUDIO"
            }
        };
        _dbContext.Farmacias.Add(farmacia);
        await _dbContext.SaveChangesAsync();

        var cliente = new Cliente
        {
            Id = 43,
            FarmaciaId = farmacia.Id,
            Farmacia = null!,
            WhatsAppId = "1199665544",
            Nome = "Cliente Audio",
            CodigoCliente = "CLI-0043",
            Status = ClienteStatus.Potencial,
            PerfilCompra = ClientePerfilCompraEnum.Prospecto
        };

        _mockCrmService
            .Setup(s => s.GetOrCreateClienteByWhatsApp(farmacia.Id.ToString(), "1199665544", null))
            .ReturnsAsync(cliente);

        _mockCryptoService
            .Setup(s => s.DecryptMedia(It.IsAny<byte[]>(), It.IsAny<string?>(), It.IsAny<string>()))
            .Returns((byte[]?)null);

        var handler = new RoutingFakeHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Get &&
                request.RequestUri?.Host.Contains("whatsapp.net", StringComparison.OrdinalIgnoreCase) == true)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(new byte[] { 0x10, 0x20, 0x30, 0x40 })
                    {
                        Headers = { { "Content-Type", "audio/ogg" } }
                    }
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{}")
            };
        });

        _mockHttpClientFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handler));

        var cdnUrl = "https://mmg.whatsapp.net/v/t62.7119-24/test-encrypted-audio.enc";
        var payload = new Dictionary<string, object>
        {
            ["event"] = "MESSAGES_UPSERT",
            ["instanceName"] = "CHAVEAUDIO",
            ["data"] = new
            {
                key = new
                {
                    remoteJid = "551199665544@s.whatsapp.net",
                    fromMe = false,
                    id = "3EB0AUD01"
                },
                messageType = "audioMessage",
                message = new
                {
                    audioMessage = new
                    {
                        url = cdnUrl,
                        mediaKey = "AQIDBA==",
                        mimetype = "audio/ogg"
                    }
                }
            }
        };

        var result = await _controller.ProcessWebhook(payload);

        result.Should().BeOfType<OkObjectResult>();
        handler.RequestedUrls.Should().ContainSingle(url => url == "/v/t62.7119-24/test-encrypted-audio.enc");
        var mensagem = await _dbContext.Mensagens.SingleAsync();
        mensagem.MediaUrl.Should().NotBeNullOrWhiteSpace();
        mensagem.MediaUrl.Should().Be(cdnUrl);
    }

    [Fact]
    public async Task ProcessWebhook_WithTextMessage_DoesNotInvokeEvolutionMediaDownloadEndpoints()
    {
        var farmacia = new Farmacia
        {
            Nome = "Farmácia Texto Test",
            Cnpj = "66666666000164",
            Telefone = "(11) 4000-2222",
            Email = "texto@farmacia.com",
            Endereco = "Rua Texto, 40",
            Cidade = "São Paulo",
            Estado = "SP",
            Cep = "01200-000",
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "CHAVETEXTO",
                WhatsAppApiUrl = "https://evolution.exemplo.com",
                WhatsAppApiKey = "api-key"
            }
        };
        _dbContext.Farmacias.Add(farmacia);
        await _dbContext.SaveChangesAsync();

        var cliente = new Cliente
        {
            Id = 44,
            FarmaciaId = farmacia.Id,
            Farmacia = null!,
            WhatsAppId = "1199554433",
            Nome = "Cliente Texto",
            CodigoCliente = "CLI-0044",
            Status = ClienteStatus.Potencial,
            PerfilCompra = ClientePerfilCompraEnum.Prospecto
        };

        _mockCrmService
            .Setup(s => s.GetOrCreateClienteByWhatsApp(farmacia.Id.ToString(), "1199554433", null))
            .ReturnsAsync(cliente);

        var handler = new RoutingFakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}")
        });

        _mockHttpClientFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handler));

        var payload = new Dictionary<string, object>
        {
            ["event"] = "MESSAGES_UPSERT",
            ["instanceName"] = "CHAVETEXTO",
            ["data"] = new
            {
                key = new
                {
                    remoteJid = "551199554433@s.whatsapp.net",
                    fromMe = false,
                    id = "3EB0TXT01"
                },
                message = new
                {
                    conversation = "teste, pode silenciar ai rsrs"
                }
            }
        };

        var result = await _controller.ProcessWebhook(payload);

        result.Should().BeOfType<OkObjectResult>();
        handler.RequestedUrls.Should().BeEmpty();
    }

    [Fact]
    public void BuildDownloadImageRequestJson_UsesDocumentedMessageWrapperPayload()
    {
        const string rawPayload =
            """
            {
              "key": {
                "remoteJid": "551199776655@s.whatsapp.net",
                "fromMe": false,
                "id": "3EB0DOWNLOADIMAGE01"
              },
              "messageType": "imageMessage",
              "message": {
                "imageMessage": {
                  "directPath": "/v/t62.7119-24/image.enc",
                  "mediaKey": [1, 2, 3, 4],
                  "fileSHA256": [5, 6, 7, 8],
                  "fileEncSHA256": [9, 10, 11, 12],
                  "mimetype": "image/jpeg",
                  "caption": "Foto via fallback"
                }
              }
            }
            """;

        WhatsAppController.TryBuildDownloadMessageJson(rawPayload, out var messageJson).Should().BeTrue();
        var requestBody = WhatsAppController.BuildDownloadImageRequestJson(
            messageJson, "image/jpeg", null, null, null, null, null);

        requestBody.Should().NotBeNullOrWhiteSpace();
        using var requestJson = System.Text.Json.JsonDocument.Parse(requestBody!);
        var message = requestJson.RootElement.GetProperty("message");
        message.TryGetProperty("imageMessage", out var imageMessage).Should().BeTrue();
        imageMessage.GetProperty("mediaKey").EnumerateArray().Select(v => v.GetInt32()).Should().Equal(1, 2, 3, 4);
        requestJson.RootElement.TryGetProperty("directPath", out _).Should().BeFalse();
    }

    [Fact]
    public void BuildDownloadImageRequestJson_WithMetadataFields_NestsFieldsUnderMediaWrapper()
    {
        var requestBody = WhatsAppController.BuildDownloadImageRequestJson(
            null,
            "image/jpeg",
            "AQIDBA==",
            null,
            "BQYHCA==",
            "CQoLDA==",
            "/v/t62.7119-24/image.enc");

        requestBody.Should().NotBeNullOrWhiteSpace();
        using var requestJson = System.Text.Json.JsonDocument.Parse(requestBody!);
        var root = requestJson.RootElement;
        root.TryGetProperty("message", out var message).Should().BeTrue();
        message.TryGetProperty("imageMessage", out var imageMessage).Should().BeTrue();
        imageMessage.GetProperty("directPath").GetString().Should().Be("/v/t62.7119-24/image.enc");
        imageMessage.GetProperty("mediaKey").EnumerateArray().Select(v => v.GetInt32()).Should().Equal(1, 2, 3, 4);
        imageMessage.GetProperty("fileSHA256").EnumerateArray().Select(v => v.GetInt32()).Should().Equal(5, 6, 7, 8);
        imageMessage.GetProperty("fileEncSHA256").EnumerateArray().Select(v => v.GetInt32()).Should().Equal(9, 10, 11, 12);
        imageMessage.GetProperty("mimetype").GetString().Should().Be("image/jpeg");
        root.TryGetProperty("directPath", out _).Should().BeFalse();
    }

    [Fact]
    public async Task ProcessWebhook_WithNonCdnMediaUrl_KeepsOriginalUrl()
    {
        var farmacia = new Farmacia
        {
            Nome = "Farmácia Non-CDN Test",
            Cnpj = "33333333000177",
            Telefone = "(21) 5555-6666",
            Email = "noncdn@farmacia.com",
            Endereco = "Rua C, 789",
            Cidade = "Curitiba",
            Estado = "PR",
            Cep = "80000-000",
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "CHAVENONCDN"
            }
        };
        _dbContext.Farmacias.Add(farmacia);
        await _dbContext.SaveChangesAsync();

        var cliente = new Cliente
        {
            Id = 41,
            FarmaciaId = farmacia.Id,
            Farmacia = null!,
            WhatsAppId = "1155443322",
            Nome = "Cliente Non-CDN",
            CodigoCliente = "CLI-0041",
            Status = ClienteStatus.Potencial,
            PerfilCompra = ClientePerfilCompraEnum.Prospecto
        };

        _mockCrmService
            .Setup(s => s.GetOrCreateClienteByWhatsApp(farmacia.Id.ToString(), "1155443322", null))
            .ReturnsAsync(cliente);

        // No HttpClient setup — if CreateClient is called the test will use the default mock (returns null),
        // which would throw. This verifies that no HTTP call is made for non-CDN URLs.
        _mockHttpClientFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Throws(new InvalidOperationException("HttpClient should not be created for non-CDN URLs"));

        var nonCdnUrl = "https://media.exemplo.com/imagem.jpg";
        var payload = new Dictionary<string, object>
        {
            ["event"] = "MESSAGES_UPSERT",
            ["instanceName"] = "CHAVENONCDN",
            ["data"] = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                $$"""
                {
                  "key": {
                    "remoteJid": "551155443322@s.whatsapp.net",
                    "fromMe": false,
                    "id": "3EB0NONCDN01"
                  },
                  "message": {
                    "imageMessage": {
                      "url": "{{nonCdnUrl}}",
                      "caption": "Foto externa",
                      "fileLength": "512"
                    }
                  }
                }
                """)
        };

        var result = await _controller.ProcessWebhook(payload);

        result.Should().BeOfType<OkObjectResult>();
        var mensagem = await _dbContext.Mensagens.SingleAsync();
        mensagem.MediaUrl.Should().Be(nonCdnUrl);
    }

    [Fact]
    public async Task FixExistingMediaUrls_WithCorruptedLocalMedia_ClearsMediaUrlWhenReprocessFails()
    {
        var farmacia = new Farmacia
        {
            Nome = "Farmácia Cleanup Test",
            Cnpj = "44444444000166",
            Telefone = "(31) 4444-1111",
            Email = "cleanup@farmacia.com",
            Endereco = "Rua Cleanup, 22",
            Cidade = "Belo Horizonte",
            Estado = "MG",
            Cep = "30000-000",
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "CHAVECLEANUP"
            }
        };
        _dbContext.Farmacias.Add(farmacia);
        await _dbContext.SaveChangesAsync();

        var atendimento = new Atendimento
        {
            FarmaciaId = farmacia.Id,
            Farmacia = null!,
            ClienteId = null,
            Cliente = null!,
            WhatsAppId = "5511999999999",
            CanalEnum = CanalEnum.WhatsApp,
            StatusEnum = AtendimentoStatusEnum.Pendente,
            DataInicio = DateTime.UtcNow
        };
        _dbContext.Atendimentos.Add(atendimento);
        await _dbContext.SaveChangesAsync();

        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), Consts.Midia.UploadsRelativePath);
        Directory.CreateDirectory(uploadsDir);
        var fileName = $"{Guid.NewGuid()}.jpg";
        var filePath = Path.Combine(uploadsDir, fileName);
        try
        {
            await System.IO.File.WriteAllBytesAsync(filePath, new byte[] { 0x01, 0x02, 0x03, 0x04 }); // invalid JPEG

            var mensagem = new Mensagem
            {
                AtendimentoId = atendimento.Id,
                MessageId = "cleanup-001",
                Direction = MessageDirection.Inbound,
                Type = MessageType.Image,
                Status = MessageStatus.Recebida,
                MediaType = "image/jpeg",
                MediaUrl = $"{Consts.Midia.UploadUrlPrefix}{fileName}",
                ReceivedAt = DateTime.UtcNow
            };
            _dbContext.Mensagens.Add(mensagem);
            await _dbContext.SaveChangesAsync();

            _controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim("FarmaciaId", farmacia.Id.ToString()) }, "test"))
                }
            };

            var result = await _controller.FixExistingMediaUrls(maxCount: 10);

            result.Should().BeOfType<OkObjectResult>();
            var persisted = await _dbContext.Mensagens.AsNoTracking().SingleAsync(m => m.Id == mensagem.Id);
            persisted.MediaUrl.Should().BeNull();
        }
        finally
        {
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);
        }
    }

    /// <summary>Minimal HttpMessageHandler stub that always returns the provided response.</summary>
    private sealed class FakeHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            System.Threading.CancellationToken cancellationToken)
            => Task.FromResult(response);
    }

    private sealed class RoutingFakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<string> RequestedUrls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            System.Threading.CancellationToken cancellationToken)
        {
            RequestedUrls.Add(request.RequestUri?.AbsolutePath ?? string.Empty);
            return Task.FromResult(responder(request));
        }
    }

}
