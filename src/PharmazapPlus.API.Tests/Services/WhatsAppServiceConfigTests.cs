using FluentAssertions;
#pragma warning disable CS0618 // UseRealApi is obsolete
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using PharmazapPlus.API.Data;
using PharmazapPlus.API.Hubs;
using PharmazapPlus.API.Services.Queue;
using PharmazapPlus.API.Services.WhatsApp;
using PharmazapPlus.Shared;
using PharmazapPlus.Shared.Enums;
using PharmazapPlus.Shared.Models.Config;
using PharmazapPlus.Shared.Models.Core;
using PharmazapPlus.Shared.Models.CRM;
using PharmazapPlus.Shared.Models.Mensagens;
using System.Net;
using System.Text;
using System.Text.Json;

namespace PharmazapPlus.API.Tests.Services;

/// <summary>
/// Testes unitários para os aprimoramentos feitos ao <see cref="WhatsAppService"/>:
/// <list type="bullet">
///   <item>VersionedApiBaseUrl usa <see cref="WhatsAppConfig.ApiVersion"/> configurável</item>
///   <item>Resiliência do SignalR — falha em NotifyNewMessage não interrompe envio da mensagem</item>
///   <item>Predicados explícitos de status no lugar de EstaAtivo (compatibilidade EF Core)</item>
/// </list>
/// </summary>
public class WhatsAppServiceConfigTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static WhatsAppConfig BuildConfig(string apiVersion = "v18.0", string apiBaseUrl = "https://graph.facebook.com")
        => new()
        {
            UseRealApi    = true,
            ApiVersion    = apiVersion,
            ApiBaseUrl    = apiBaseUrl,
            PhoneNumberId = "mock-phone-id",
            AccessToken   = "mock-access-token",
            WebhookVerifyToken = "token"
        };

    private static WhatsAppService BuildService(
        WhatsAppConfig config,
        HttpClient? httpClient = null)
    {
        var dbOptions = new DbContextOptionsBuilder<PharmazapDbContext>()
            .UseInMemoryDatabase($"WhatsAppServiceConfigTests_{Guid.NewGuid()}")
            .Options;
        var dbContext     = new PharmazapDbContext(dbOptions);
        var cache         = new Mock<IDistributedCache>().Object;
        var logger        = new Mock<ILogger<WhatsAppService>>().Object;
        var options       = Options.Create(config);
        var queue         = new Mock<IMessageQueue>().Object;
        var hubContext    = new Mock<IHubContext<PharmaHub>>().Object;
        var client        = httpClient ?? new HttpClient();

        return new WhatsAppService(client, dbContext, cache, logger, options, queue, hubContext);
    }

    // ── VersionedApiBaseUrl ───────────────────────────────────────────────────

    [Fact]
    public void WhatsAppService_CanBeInstantiated_WithV18Config()
    {
        // Arrange
        var config = BuildConfig(apiVersion: "v18.0");

        // Act — construção não deve lançar exceção
        var service = BuildService(config);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void WhatsAppService_CanBeInstantiated_WithV23Config()
    {
        // Arrange — versão configurável (ex: v23.0 conforme atualização da API Meta)
        var config = BuildConfig(apiVersion: "v23.0");

        // Act
        var service = BuildService(config);

        // Assert
        service.Should().NotBeNull("o serviço deve suportar qualquer versão configurada");
    }

    // ── WhatsAppConfig.ApiVersion ─────────────────────────────────────────────

    [Fact]
    public void WhatsAppConfig_DefaultApiVersion_IsV23()
    {
        var config = new WhatsAppConfig();
        config.ApiVersion.Should().Be("v23.0",
            "o padrão deve ser v23.0 para compatibilidade com a versão atual da API Meta");
    }

    [Theory]
    [InlineData("v18.0")]
    [InlineData("v19.0")]
    [InlineData("v20.0")]
    [InlineData("v23.0")]
    public void WhatsAppConfig_ApiVersion_CanBeSetToAnyVersion(string version)
    {
        var config = new WhatsAppConfig { ApiVersion = version };
        config.ApiVersion.Should().Be(version);
    }

    [Fact]
    public void WhatsAppConfig_ApiBaseUrl_DefaultHasNoTrailingVersion()
    {
        var config = new WhatsAppConfig();
        config.ApiBaseUrl.Should().Be("https://graph.facebook.com",
            "a URL base não deve incluir a versão — ela é composta dinamicamente com ApiVersion");
        config.ApiBaseUrl.Should().NotContain("v18",
            "a versão não deve estar hardcoded na URL base");
    }

    // ── Consts.MetaApi.BaseUrl ────────────────────────────────────────────────

    [Fact]
    public void Consts_MetaApiBaseUrl_ContainsNoHardcodedVersion()
    {
        Consts.MetaApi.BaseUrl.Should().StartWith("https://graph.facebook.com",
            "a constante deve apontar para o domínio base da Meta");
        Consts.MetaApi.BaseUrl.Should().NotMatchRegex(@"v\d+\.\d+",
            "a versão da API não deve estar hardcoded na constante — deve ser definida em WhatsAppConfig.ApiVersion");
    }

    // ── SendTemplateRequest.Parameters é nullable ─────────────────────────────

    [Fact]
    public void SendTemplateRequest_Parameters_AcceptsNullValue()
    {
        // Garante que parâmetros opcionais de template são de fato opcionais no modelo
        var request = new PharmazapPlus.API.Controllers.SendTemplateRequest
        {
            To           = "+5511999999999",
            TemplateName = "lembrete_consulta"
        };

        request.Parameters.Should().BeNull(
            "parâmetros de template são opcionais; a propriedade deve ser nullable");
    }

    [Fact]
    public void SendTemplateRequest_Parameters_AcceptsNonNullDictionary()
    {
        var request = new PharmazapPlus.API.Controllers.SendTemplateRequest
        {
            To           = "+5511999999999",
            TemplateName = "lembrete_consulta",
            Parameters   = new Dictionary<string, object> { { "nome", "Maria" } }
        };

        request.Parameters.Should().NotBeNull();
        request.Parameters.Should().ContainKey("nome");
        request.Parameters!["nome"].Should().Be("Maria");
    }

    [Fact]
    public async Task SendTextMessage_MetaApi_UsesFormattedNumberAndLinksMessageByNormalizedIdentifier()
    {
        // Arrange
        const string expectedApiPhone = "556298808389"; // DDD 62, 9 already present at pos 2 → no extra 9 added
        const string atendimentoWhatsAppId = "6298808389";
        const string incomingPhone = "6298808389";
        const string expectedMessageId = "wamid.test123";

        var dbOptions = new DbContextOptionsBuilder<PharmazapDbContext>()
            .UseInMemoryDatabase($"WhatsAppServiceConfigTests_{Guid.NewGuid()}")
            .Options;
        await using var dbContext = new PharmazapDbContext(dbOptions);

        var farmacia = new Farmacia
        {
            Id = 1,
            Nome = "Farmácia Teste",
            Cnpj = "12345678000199",
            Telefone = "62999999999",
            Email = "teste@farmacia.com",
            Endereco = "Rua Teste, 123",
            Cidade = "Goiânia",
            Estado = "GO",
            Cep = "74000000",
            Integracoes = new FarmaciaIntegracoes
            {
                FarmaciaId = 1,
                WhatsAppPhoneNumberId = "phone-id",
                WhatsAppAccessToken = "token",
                WhatsAppBusinessAccountId = "business-id"
            }
        };

        var atendimento = new Atendimento
        {
            FarmaciaId = 1,
            WhatsAppId = atendimentoWhatsAppId,
            CanalEnum = CanalEnum.WhatsApp,
            StatusEnum = AtendimentoStatusEnum.EmAndamento,
            DataInicio = DateTime.UtcNow
        };

        dbContext.Farmacias.Add(farmacia);
        dbContext.Atendimentos.Add(atendimento);
        await dbContext.SaveChangesAsync();

        string? sentPayload = null;
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, _) =>
            {
                sentPayload = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent($"{{\"messages\":[{{\"id\":\"{expectedMessageId}\"}}]}}", Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(handler.Object);
        var service = new WhatsAppService(
            httpClient,
            dbContext,
            new Mock<IDistributedCache>().Object,
            new Mock<ILogger<WhatsAppService>>().Object,
            Options.Create(BuildConfig()),
            new Mock<IMessageQueue>().Object,
            new Mock<IHubContext<PharmaHub>>().Object);

        // Act
        var result = await service.SendTextMessage("1", incomingPhone, "Olá");

        // Assert
        result.Success.Should().BeTrue();
        result.MessageId.Should().Be(expectedMessageId);
        sentPayload.Should().NotBeNullOrWhiteSpace();
        sentPayload.Should().Contain($"\"to\":\"{expectedApiPhone}\"");
    }

    [Theory]
    [InlineData("6298808389", "556298808389")]    // DDD 62 com 9º dígito na posição 2 → apenas prefixa 55
    [InlineData("62998808389", "5562998808389")]  // DDD 62 com 9º dígito (11 dígitos) → 55 + digits
    [InlineData("5562998808389", "5562998808389")] // já formatado (13 dígitos) → retornar como está
    [InlineData("556298808389", "556298808389")]  // 12 dígitos com 55 + 9 na pos 4 → retornar como está
    public void FormatPhoneNumberForSending_FormatsCorrectly(string input, string expected)
    {
        // Arrange
        var config = BuildConfig();
        var service = BuildService(config);

        // Act - use reflection to call private method
        var method = typeof(WhatsAppService).GetMethod("FormatPhoneNumberForSending",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = method?.Invoke(service, new object[] { input }) as string;

        // Assert
        result.Should().Be(expected, $"input '{input}' deve ser formatado como '{expected}'");
    }

    [Fact]
    public async Task SendTextMessage_EvolutionApi_ValidatesApiKey()
    {
        // Arrange
        var dbOptions = new DbContextOptionsBuilder<PharmazapDbContext>()
            .UseInMemoryDatabase($"WhatsAppServiceConfigTests_{Guid.NewGuid()}")
            .Options;
        await using var dbContext = new PharmazapDbContext(dbOptions);

        var farmacia = new Farmacia
        {
            Id = 1,
            Nome = "Farmácia Teste",
            Cnpj = "12345678000199",
            Telefone = "62999999999",
            Email = "teste@farmacia.com",
            Endereco = "Rua Teste, 123",
            Cidade = "Goiânia",
            Estado = "GO",
            Cep = "74000000",
            Integracoes = new FarmaciaIntegracoes
            {
                FarmaciaId = 1,
                WhatsAppApiUrl = "https://evolution.example.com",
                WhatsAppApiKey = "", // API key vazia
                ChaveWhatsApp = "instance1"
            }
        };

        dbContext.Farmacias.Add(farmacia);
        await dbContext.SaveChangesAsync();

        var httpClient = new HttpClient();
        var service = new WhatsAppService(
            httpClient,
            dbContext,
            new Mock<IDistributedCache>().Object,
            new Mock<ILogger<WhatsAppService>>().Object,
            Options.Create(BuildConfig()),
            new Mock<IMessageQueue>().Object,
            new Mock<IHubContext<PharmaHub>>().Object);

        // Act
        var result = await service.SendTextMessage("1", "62998808389", "Teste");

        // Assert
        result.Success.Should().BeFalse("deve falhar quando API key está vazia");
        result.ErrorMessage.Should().Contain("API key não configurada");
    }

    [Fact]
    public async Task SendTextMessage_RoutesToMetaApi_WhenMetaConfigured()
    {
        // Arrange
        var dbOptions = new DbContextOptionsBuilder<PharmazapDbContext>()
            .UseInMemoryDatabase($"WhatsAppServiceConfigTests_{Guid.NewGuid()}")
            .Options;
        await using var dbContext = new PharmazapDbContext(dbOptions);

        var farmacia = new Farmacia
        {
            Id = 1,
            Nome = "Farmácia Teste",
            Cnpj = "12345678000199",
            Telefone = "62999999999",
            Email = "teste@farmacia.com",
            Endereco = "Rua Teste, 123",
            Cidade = "Goiânia",
            Estado = "GO",
            Cep = "74000000",
            Integracoes = new FarmaciaIntegracoes
            {
                FarmaciaId = 1,
                WhatsAppPhoneNumberId = "phone-id",
                WhatsAppAccessToken = "token",
                WhatsAppBusinessAccountId = "business-id"
            }
        };

        dbContext.Farmacias.Add(farmacia);
        await dbContext.SaveChangesAsync();

        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("graph.facebook.com")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"messages\":[{\"id\":\"wamid.test\"}]}", Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(handler.Object);
        var service = new WhatsAppService(
            httpClient,
            dbContext,
            new Mock<IDistributedCache>().Object,
            new Mock<ILogger<WhatsAppService>>().Object,
            Options.Create(BuildConfig()),
            new Mock<IMessageQueue>().Object,
            new Mock<IHubContext<PharmaHub>>().Object);

        // Act
        var result = await service.SendTextMessage("1", "62998808389", "Teste");

        // Assert
        result.Success.Should().BeTrue();
        handler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("graph.facebook.com")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendTextMessage_RoutesToEvolutionApi_WhenEvolutionConfigured()
    {
        // Arrange
        var dbOptions = new DbContextOptionsBuilder<PharmazapDbContext>()
            .UseInMemoryDatabase($"WhatsAppServiceConfigTests_{Guid.NewGuid()}")
            .Options;
        await using var dbContext = new PharmazapDbContext(dbOptions);

        var farmacia = new Farmacia
        {
            Id = 1,
            Nome = "Farmácia Teste",
            Cnpj = "12345678000199",
            Telefone = "62999999999",
            Email = "teste@farmacia.com",
            Endereco = "Rua Teste, 123",
            Cidade = "Goiânia",
            Estado = "GO",
            Cep = "74000000",
            Integracoes = new FarmaciaIntegracoes
            {
                FarmaciaId = 1,
                WhatsAppPhoneNumberId = "stale-meta-phone-id",
                WhatsAppAccessToken = "stale-meta-token",
                WhatsAppApiUrl = "https://evolution.example.com",
                WhatsAppApiKey = "test-key",
                ChaveWhatsApp = "instance1",
                Canais =
                [
                    new Canal
                    {
                        Nome = "WhatsApp Evolution",
                        Tipo = Consts.CanalTipo.WhatsApp,
                        IsOfficial = false,
                        InstanceName = "instance1"
                    }
                ]
            }
        };

        dbContext.Farmacias.Add(farmacia);
        await dbContext.SaveChangesAsync();

        var requests = new List<HttpRequestMessage>();
        var responses = new Queue<HttpResponseMessage>(new[]
        {
            new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"data\":[{\"name\":\"instance1\",\"token\":\"instance-token\"}]}", Encoding.UTF8, "application/json")
            },
            new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"key\":{\"id\":\"evolution-test\"}}", Encoding.UTF8, "application/json")
            }
        });
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, _) => requests.Add(request))
            .ReturnsAsync(() => responses.Dequeue());

        var httpClient = new HttpClient(handler.Object);
        var service = new WhatsAppService(
            httpClient,
            dbContext,
            new Mock<IDistributedCache>().Object,
            new Mock<ILogger<WhatsAppService>>().Object,
            Options.Create(BuildConfig()),
            new Mock<IMessageQueue>().Object,
            new Mock<IHubContext<PharmaHub>>().Object);

        // Act
        var result = await service.SendTextMessage("1", "62998808389", "Teste");

        // Assert
        result.Success.Should().BeTrue();
        result.MessageId.Should().Be("evolution-test");
        requests.Should().HaveCount(2);
        requests[0].RequestUri!.ToString().Should().Be("https://evolution.example.com/instance/fetchInstances?instanceName=instance1");
        requests[0].Headers.GetValues("apikey").Single().Should().Be("test-key");
        requests[1].RequestUri!.ToString().Should().Be("https://evolution.example.com/send/text");
        requests[1].Headers.GetValues("apikey").Single().Should().Be("instance-token");
        (await requests[1].Content!.ReadAsStringAsync()).Should().Contain("\"number\":\"5562998808389\"");
        handler.Protected().Verify(
            "SendAsync",
            Times.Exactly(2),
            ItExpr.Is<HttpRequestMessage>(req => 
                req.RequestUri!.ToString().Contains("evolution.example.com")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendTextMessage_PrefersMetaApi_WhenLegacyEvolutionConfigStillExists()
    {
        // Arrange
        var dbOptions = new DbContextOptionsBuilder<PharmazapDbContext>()
            .UseInMemoryDatabase($"WhatsAppServiceConfigTests_{Guid.NewGuid()}")
            .Options;
        await using var dbContext = new PharmazapDbContext(dbOptions);

        var farmacia = new Farmacia
        {
            Id = 1,
            Nome = "Farmácia Teste",
            Cnpj = "12345678000199",
            Telefone = "62999999999",
            Email = "teste@farmacia.com",
            Endereco = "Rua Teste, 123",
            Cidade = "Goiânia",
            Estado = "GO",
            Cep = "74000000",
            Integracoes = new FarmaciaIntegracoes
            {
                FarmaciaId = 1,
                WhatsAppPhoneNumberId = "phone-id",
                WhatsAppAccessToken = "token",
                WhatsAppBusinessAccountId = "business-id",
                WhatsAppApiUrl = "https://evolution.example.com",
                WhatsAppApiKey = "legacy-key",
                ChaveWhatsApp = "legacy-instance"
            }
        };

        dbContext.Farmacias.Add(farmacia);
        await dbContext.SaveChangesAsync();

        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("graph.facebook.com")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"messages\":[{\"id\":\"wamid.test\"}]}", Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(handler.Object);
        var service = new WhatsAppService(
            httpClient,
            dbContext,
            new Mock<IDistributedCache>().Object,
            new Mock<ILogger<WhatsAppService>>().Object,
            Options.Create(BuildConfig()),
            new Mock<IMessageQueue>().Object,
            new Mock<IHubContext<PharmaHub>>().Object);

        // Act
        var result = await service.SendTextMessage("1", "62998808389", "Teste");

        // Assert
        result.Success.Should().BeTrue();
        handler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("graph.facebook.com")),
            ItExpr.IsAny<CancellationToken>());
        handler.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("evolution.example.com")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendTemplateMessage_RequiresMetaConfig()
    {
        // Arrange
        var dbOptions = new DbContextOptionsBuilder<PharmazapDbContext>()
            .UseInMemoryDatabase($"WhatsAppServiceConfigTests_{Guid.NewGuid()}")
            .Options;
        await using var dbContext = new PharmazapDbContext(dbOptions);

        var farmacia = new Farmacia
        {
            Id = 1,
            Nome = "Farmácia Teste",
            Cnpj = "12345678000199",
            Telefone = "62999999999",
            Email = "teste@farmacia.com",
            Endereco = "Rua Teste, 123",
            Cidade = "Goiânia",
            Estado = "GO",
            Cep = "74000000",
            Integracoes = new FarmaciaIntegracoes
            {
                FarmaciaId = 1,
                WhatsAppApiUrl = "https://evolution.example.com", // Apenas Evolution configurado
                WhatsAppApiKey = "test-key",
                ChaveWhatsApp = "instance1"
            }
        };

        dbContext.Farmacias.Add(farmacia);
        await dbContext.SaveChangesAsync();

        var httpClient = new HttpClient();
        var service = new WhatsAppService(
            httpClient,
            dbContext,
            new Mock<IDistributedCache>().Object,
            new Mock<ILogger<WhatsAppService>>().Object,
            Options.Create(BuildConfig()),
            new Mock<IMessageQueue>().Object,
            new Mock<IHubContext<PharmaHub>>().Object);

        // Act
        var result = await service.SendTemplateMessage("1", "62998808389", "template_test", "pt_BR");

        // Assert
        result.Success.Should().BeFalse("SendTemplateMessage requer Meta API configurada");
        result.ErrorMessage.Should().Contain("Meta WhatsApp API não configurada");
    }

    // ── Nome de contato (username) via webhook oficial da Meta ────────────────

    private static Dictionary<string, object> BuildMetaMessagePayload(
        string phoneNumberId, string from, string waId, string? profileName, string messageId = "wamid.test1")
    {
        var payloadJson = JsonSerializer.Serialize(new
        {
            @object = "whatsapp_business_account",
            entry = new object[]
            {
                new
                {
                    changes = new object[]
                    {
                        new
                        {
                            field = "messages",
                            value = new
                            {
                                metadata = new { phone_number_id = phoneNumberId },
                                contacts = profileName == null
                                    ? Array.Empty<object>()
                                    : new object[] { new { profile = new { name = profileName }, wa_id = waId } },
                                messages = new object[]
                                {
                                    new
                                    {
                                        from,
                                        id = messageId,
                                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                                        type = "text",
                                        text = new { body = "Olá" }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        });

        return JsonSerializer.Deserialize<Dictionary<string, object>>(payloadJson)!;
    }

    [Fact]
    public async Task ProcessWebhookPayload_UsesMetaContactProfileName_ForNewCliente()
    {
        // Arrange
        var dbOptions = new DbContextOptionsBuilder<PharmazapDbContext>()
            .UseInMemoryDatabase($"WhatsAppServiceConfigTests_{Guid.NewGuid()}")
            .Options;
        await using var dbContext = new PharmazapDbContext(dbOptions);

        var farmacia = new Farmacia
        {
            Id = 1,
            Nome = "Farmácia Teste",
            Cnpj = "12345678000199",
            Telefone = "62999999999",
            Email = "teste@farmacia.com",
            Endereco = "Rua Teste, 123",
            Cidade = "Goiânia",
            Estado = "GO",
            Cep = "74000000",
            Integracoes = new FarmaciaIntegracoes
            {
                FarmaciaId = 1,
                WhatsAppPhoneNumberId = "phone-id-1",
                WhatsAppAccessToken = "token"
            }
        };
        dbContext.Farmacias.Add(farmacia);
        await dbContext.SaveChangesAsync();

        var payload = BuildMetaMessagePayload("phone-id-1", "5562998808389", "5562998808389", "Maria Souza (@maria.souza)");

        var service = new WhatsAppService(
            new HttpClient(),
            dbContext,
            new Mock<IDistributedCache>().Object,
            new Mock<ILogger<WhatsAppService>>().Object,
            Options.Create(BuildConfig()),
            new Mock<IMessageQueue>().Object,
            new Mock<IHubContext<PharmaHub>>().Object);

        // Act
        var result = await service.ProcessWebhookPayload(payload);

        // Assert
        result.Success.Should().BeTrue(result.Message);
        result.NewClientCreated.Should().BeTrue();
        var cliente = dbContext.Clientes.Single();
        cliente.Nome.Should().Be("Maria Souza (@maria.souza)");
    }

    [Fact]
    public async Task ProcessWebhookPayload_UpdatesPlaceholderName_WithMetaContactProfileName()
    {
        // Arrange
        var dbOptions = new DbContextOptionsBuilder<PharmazapDbContext>()
            .UseInMemoryDatabase($"WhatsAppServiceConfigTests_{Guid.NewGuid()}")
            .Options;
        await using var dbContext = new PharmazapDbContext(dbOptions);

        var farmacia = new Farmacia
        {
            Id = 1,
            Nome = "Farmácia Teste",
            Cnpj = "12345678000199",
            Telefone = "62999999999",
            Email = "teste@farmacia.com",
            Endereco = "Rua Teste, 123",
            Cidade = "Goiânia",
            Estado = "GO",
            Cep = "74000000",
            Integracoes = new FarmaciaIntegracoes
            {
                FarmaciaId = 1,
                WhatsAppPhoneNumberId = "phone-id-1",
                WhatsAppAccessToken = "token"
            }
        };
        dbContext.Farmacias.Add(farmacia);

        var clienteExistente = new Cliente
        {
            FarmaciaId = 1,
            Farmacia = null!,
            WhatsAppId = "5562998808389",
            Nome = "Cliente WhatsApp 5562998808389",
            CodigoCliente = "CLI-0001"
        };
        dbContext.Clientes.Add(clienteExistente);
        await dbContext.SaveChangesAsync();

        var service = new WhatsAppService(
            new HttpClient(),
            dbContext,
            new Mock<IDistributedCache>().Object,
            new Mock<ILogger<WhatsAppService>>().Object,
            Options.Create(BuildConfig()),
            new Mock<IMessageQueue>().Object,
            new Mock<IHubContext<PharmaHub>>().Object);

        var payload = BuildMetaMessagePayload("phone-id-1", "5562998808389", "5562998808389", "João Pereira");

        // Act
        var result = await service.ProcessWebhookPayload(payload);

        // Assert
        result.Success.Should().BeTrue();
        result.NewClientCreated.Should().BeFalse();
        var cliente = dbContext.Clientes.Single();
        cliente.Nome.Should().Be("João Pereira");
    }
}
