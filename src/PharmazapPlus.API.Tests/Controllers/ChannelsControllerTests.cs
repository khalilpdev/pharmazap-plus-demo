using System.Security.Claims;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using PharmazapPlus.API.Controllers;
using PharmazapPlus.API.Data;
using PharmazapPlus.API.Services.Security;
using PharmazapPlus.Shared;
using PharmazapPlus.Shared.Enums;
using PharmazapPlus.Shared.Models.Core;
using Xunit;

namespace PharmazapPlus.API.Tests.Controllers;

public class ChannelsControllerTests
{
    private PharmazapDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PharmazapDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        var context = new PharmazapDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private ClaimsPrincipal CreateUserClaimsPrincipal(int farmaciaId, string role = UserRoles.Desenvolvedor)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "1"),
            new("FarmaciaId", farmaciaId.ToString()),
            new(ClaimTypes.Role, role)
        };

        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }

    private ChannelsController CreateController(PharmazapDbContext context, int farmaciaId, IHttpClientFactory? httpClientFactory = null, IConfiguration? configuration = null, IWebHostEnvironment? webHostEnvironment = null)
    {
        var loggerFactory = new LoggerFactory();
        var logger = loggerFactory.CreateLogger<ChannelsController>();
        var cryptoService = CryptoService.FromBase64Key(CryptoService.GenerateBase64Key());
        var controller = new ChannelsController(
            context,
            logger,
            cryptoService,
            httpClientFactory ?? new Mock<IHttpClientFactory>().Object,
            configuration ?? new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GLOBAL_API_KEY"] = "test-global-api-key"
            }).Build(),
            webHostEnvironment)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreateUserClaimsPrincipal(farmaciaId) }
            }
        };
        return controller;
    }

    private static IWebHostEnvironment CreateWebHostEnvironment(string webRootPath)
    {
        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(x => x.WebRootPath).Returns(webRootPath);
        env.SetupGet(x => x.ContentRootPath).Returns(webRootPath);
        env.SetupGet(x => x.EnvironmentName).Returns("Development");
        return env.Object;
    }

    private Farmacia CreateTestFarmacia()
    {
        return new Farmacia
        {
            Nome = "Farmacia Teste",
            Cnpj = "12345678901234",
            Email = "test@test.com",
            Telefone = "(11) 99999-9999",
            Endereco = "Rua Teste, 123",
            Cidade = "São Paulo",
            Estado = "SP",
            Cep = "01310-100",
            Status = FarmaciaStatus.Ativa,
            Ativa = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Integracoes = new FarmaciaIntegracoes { CreatedAt = DateTime.UtcNow, ChaveWhatsApp = "test-instance" }
        };
    }

    [Fact]
    public async Task CreateChannel_WithInvalidEvolutionUrl_ContainingWebhook_ReturnsBadRequest()
    {
        // Arrange
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var controller = CreateController(context, farmacia.Id);

        var request = new CreateChannelRequest(
            Name: "Test WhatsApp",
            Type: 1, // WhatsApp
            IsOfficial: false,
            AccessToken: null,
            PhoneNumberId: null,
            InstagramBusinessId: null,
            BusinessAccountId: null,
            ApiUrl: "http://108.174.150.4:6000/api/whatsapp/webhook", // ❌ INVÁLIDO - contém /webhook
            ApiKey: "test-api-key",
            InstanceName: "test-instance",
            InstagramUsername: null,
            InstagramPassword: null);

        // Act
        var result = await controller.CreateChannel(request);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var response = badRequest.Value as dynamic;
        var error = response?.error?.ToString() ?? "";

        Assert.NotNull(error);
        Assert.Contains("/webhook", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("URL base", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateChannel_WithValidEvolutionUrl_Succeeds()
    {
        // Arrange
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var controller = CreateController(context, farmacia.Id);

        var request = new CreateChannelRequest(
            Name: "Test WhatsApp",
            Type: 1, // WhatsApp
            IsOfficial: false,
            AccessToken: null,
            PhoneNumberId: null,
            InstagramBusinessId: null,
            BusinessAccountId: null,
            ApiUrl: "http://108.174.150.4:6000/api/whatsapp", // ✅ VÁLIDO
            ApiKey: "test-api-key",
            InstanceName: "test-instance",
            InstagramUsername: null,
            InstagramPassword: null);

        // Act
        var result = await controller.CreateChannel(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value as dynamic;
        Assert.True(response?.success ?? false);
    }

    [Fact]
    public async Task CreateChannel_WithBaseEvolutionUrl_Succeeds()
    {
        // Arrange
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var controller = CreateController(context, farmacia.Id);

        var request = new CreateChannelRequest(
            Name: "Test WhatsApp",
            Type: 1,
            IsOfficial: false,
            AccessToken: null,
            PhoneNumberId: null,
            InstagramBusinessId: null,
            BusinessAccountId: null,
            ApiUrl: "http://108.174.150.4:8080",
            ApiKey: "test-api-key",
            InstanceName: "test-instance",
            InstagramUsername: null,
            InstagramPassword: null);

        // Act
        var result = await controller.CreateChannel(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value as dynamic;
        Assert.True(response?.success ?? false);
    }

    [Theory]
    [InlineData("http://108.174.150.4:6000/api/whatsapp")] // ✅ Valid (legado)
    [InlineData("https://evolution.example.com:6000/api/whatsapp")] // ✅ Valid
    [InlineData("http://localhost:6000/api/whatsapp")] // ✅ Valid
    [InlineData("http://localhost:8080")] // ✅ Valid (raiz)
    [InlineData("https://evolution.example.com:8080")] // ✅ Valid (raiz)
    public async Task CreateChannel_WithValidUrlFormats_Succeeds(string validUrl)
    {
        // Arrange
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var controller = CreateController(context, farmacia.Id);

        var request = new CreateChannelRequest(
            Name: "Test WhatsApp",
            Type: 1,
            IsOfficial: false,
            AccessToken: null,
            PhoneNumberId: null,
            InstagramBusinessId: null,
            BusinessAccountId: null,
            ApiUrl: validUrl,
            ApiKey: "test-api-key",
            InstanceName: "test-instance",
            InstagramUsername: null,
            InstagramPassword: null);

        // Act
        var result = await controller.CreateChannel(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value as dynamic;
        Assert.True(response?.success ?? false);
    }

    [Fact]
    public async Task CreateChannel_WithOfficialMetaMode_PersistsModeOnChannel()
    {
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var controller = CreateController(context, farmacia.Id);
        var request = new CreateChannelRequest(
            Name: "WhatsApp Oficial Teste",
            Type: 1,
            IsOfficial: true,
            AccessToken: "token-meta",
            PhoneNumberId: "123456",
            InstagramBusinessId: null,
            BusinessAccountId: "waba-1",
            ApiUrl: null,
            ApiKey: null,
            InstanceName: null,
            InstagramUsername: null,
            InstagramPassword: null,
            MetaAppMode: MetaAppMode.Test,
            MetaConfigSource: MetaConfigSource.PharmazapCentral);

        var result = await controller.CreateChannel(request);

        Assert.IsType<OkObjectResult>(result);
        var canal = await context.Canais.SingleAsync();
        Assert.Equal(MetaAppMode.Test, canal.MetaAppMode);
    }

    [Fact]
    public async Task GetChannelById_WhenOfficialChannelExists_ReturnsPersistedMetaMode()
    {
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var canal = new Canal
        {
            FarmaciaIntegracosId = farmacia.Integracoes!.Id,
            Nome = "Instagram Oficial",
            Tipo = 2,
            IsOfficial = true,
            InstagramAccountId = "ig-123",
            AccessToken = "token-meta",
            MetaConfigSource = MetaConfigSource.PharmazapCentral,
            MetaAppMode = MetaAppMode.Test
        };
        context.Canais.Add(canal);
        await context.SaveChangesAsync();

        var controller = CreateController(context, farmacia.Id);

        var result = await controller.GetChannelById(canal.Id);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var returnedMode = doc.RootElement.GetProperty("data").GetProperty("metaAppMode").GetInt32();
        Assert.Equal((int)MetaAppMode.Test, returnedMode);
    }

    [Theory]
    [InlineData("http://108.174.150.4:6000/api/whatsapp/webhook")] // ❌ Contains /webhook
    [InlineData("http://108.174.150.4:6000/api/whatsapp/webhook/")] // ❌ Contains /webhook with trailing slash
    [InlineData("http://108.174.150.4:6000/webhook/message/sendText")] // ❌ Wrong path
    [InlineData("http://108.174.150.4:8080/instance/all")] // ❌ Endpoint específico, não URL base
    public async Task CreateChannel_WithInvalidUrlFormats_ReturnsBadRequest(string invalidUrl)
    {
        // Arrange
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var controller = CreateController(context, farmacia.Id);

        var request = new CreateChannelRequest(
            Name: "Test WhatsApp",
            Type: 1,
            IsOfficial: false,
            AccessToken: null,
            PhoneNumberId: null,
            InstagramBusinessId: null,
            BusinessAccountId: null,
            ApiUrl: invalidUrl,
            ApiKey: "test-api-key",
            InstanceName: "test-instance",
            InstagramUsername: null,
            InstagramPassword: null);

        // Act
        var result = await controller.CreateChannel(request);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var response = badRequest.Value as dynamic;
        Assert.NotNull(response?.error);
    }

    [Fact]
    public async Task CreateChannel_InstagramOfficial_WithMetaCredentials_StoresAccessToken()
    {
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var controller = CreateController(context, farmacia.Id);

        var request = new CreateChannelRequest(
            Name: "Instagram Oficial",
            Type: 2,
            IsOfficial: true,
            AccessToken: "meta-token",
            PhoneNumberId: null,
            InstagramBusinessId: "17841400000000000",
            BusinessAccountId: null,
            ApiUrl: null,
            ApiKey: null,
            InstanceName: null,
            InstagramUsername: null,
            InstagramPassword: null,
            MetaConfigSource: MetaConfigSource.PharmazapCentral);

        var result = await controller.CreateChannel(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value as dynamic;
        Assert.True(response?.success ?? false);

        var canal = await context.Canais.FirstAsync(c => c.Tipo == 2);
        Assert.True(canal.IsOfficial);
        Assert.Equal("17841400000000000", canal.InstagramAccountId);
        Assert.Equal("meta-token", canal.AccessToken);
        Assert.Null(canal.ApiKey);
    }

    [Fact]
    public async Task CreateChannel_WithOwnMetaConfig_PersistsOwnMetaCredentials()
    {
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var controller = CreateController(context, farmacia.Id);
        var request = new CreateChannelRequest(
            Name: "Instagram Meta Próprio",
            Type: 2,
            IsOfficial: true,
            AccessToken: "meta-token",
            PhoneNumberId: null,
            InstagramBusinessId: "17841400000000000",
            BusinessAccountId: null,
            ApiUrl: null,
            ApiKey: null,
            InstanceName: null,
            InstagramUsername: null,
            InstagramPassword: null,
            MetaConfigSource: MetaConfigSource.Own,
            MetaAppId: "own-app-id",
            MetaAppSecret: "own-app-secret");

        var result = await controller.CreateChannel(request);

        Assert.IsType<OkObjectResult>(result);
        var canal = await context.Canais.SingleAsync();
        Assert.Equal(MetaConfigSource.Own, canal.MetaConfigSource);
        Assert.Equal("own-app-id", canal.MetaAppId);
        Assert.Equal("own-app-secret", canal.MetaAppSecret);
        Assert.Equal(MetaAppMode.Production, canal.MetaAppMode);
    }

    [Fact]
    public async Task GetChannelById_WithOwnMetaConfig_MasksSecretAndReturnsSource()
    {
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var canal = new Canal
        {
            FarmaciaIntegracosId = farmacia.Integracoes!.Id,
            Nome = "Instagram Meta Próprio",
            Tipo = 2,
            IsOfficial = true,
            InstagramAccountId = "ig-123",
            AccessToken = "token-meta",
            MetaConfigSource = MetaConfigSource.Own,
            MetaAppId = "own-app-id",
            MetaAppSecret = "own-app-secret"
        };
        context.Canais.Add(canal);
        await context.SaveChangesAsync();

        var controller = CreateController(context, farmacia.Id);
        var result = await controller.GetChannelById(canal.Id);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");
        Assert.Equal((int)MetaConfigSource.Own, data.GetProperty("metaConfigSource").GetInt32());
        Assert.Equal("own-app-id", data.GetProperty("metaAppId").GetString());
        Assert.Equal(string.Empty, data.GetProperty("metaAppSecret").GetString());
        Assert.True(data.GetProperty("hasPersistedMetaAppSecret").GetBoolean());
    }

    [Fact]
    public async Task UpdateChannel_WithInvalidEvolutionUrl_ReturnsBadRequest()
    {
        // Arrange
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();

        var canal = new Canal
        {
            Nome = "Test WhatsApp",
            Tipo = 1,
            IsOfficial = false,
            ApiUrl = "http://108.174.150.4:6000/api/whatsapp", // Original válido
            ApiKey = "test-key",
            FarmaciaIntegracosId = 0 // Será atualizado após salvar
        };
        farmacia.Integracoes!.Canais = new List<Canal> { canal };

        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var controller = CreateController(context, farmacia.Id);

        var updateRequest = new CreateChannelRequest(
            Name: "Test WhatsApp Updated",
            Type: 1,
            IsOfficial: false,
            AccessToken: null,
            PhoneNumberId: null,
            InstagramBusinessId: null,
            BusinessAccountId: null,
            ApiUrl: "http://108.174.150.4:6000/api/whatsapp/webhook", // ❌ INVÁLIDO
            ApiKey: "test-api-key",
            InstanceName: "test-instance",
            InstagramUsername: null,
            InstagramPassword: null);

        // Act
        var result = await controller.UpdateChannelById(canal.Id, updateRequest);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var response = badRequest.Value as dynamic;
        Assert.NotNull(response?.error);
        Assert.Contains("webhook", response?.error?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateChannel_WithDuplicateInstanceName_ReturnsBadRequest()
    {
        // Arrange
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        // Create first channel with InstanceName "duplicated"
        var firstCanal = new Canal
        {
            FarmaciaIntegracosId = farmacia.Integracoes!.Id,
            Nome = "duplicated",
            Tipo = 1,
            IsOfficial = false,
            ApiUrl = "http://108.174.150.4:6000/api/whatsapp",
            ApiKey = "test-key"
        };
        context.Canais.Add(firstCanal);
        await context.SaveChangesAsync();

        var controller = CreateController(context, farmacia.Id);

        var request = new CreateChannelRequest(
            Name: "WhatsApp Not Official",
            Type: 1,
            IsOfficial: false,
            AccessToken: null,
            PhoneNumberId: null,
            InstagramBusinessId: null,
            BusinessAccountId: null,
            ApiUrl: "http://108.174.150.4:6000/api/whatsapp",
            ApiKey: "test-api-key",
            InstanceName: "  DUPLICATED  ",
            InstagramUsername: null,
            InstagramPassword: null); // Same as firstCanal

        // Act
        var result = await controller.CreateChannel(request);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var response = badRequest.Value as dynamic;
        Assert.NotNull(response?.error);
        Assert.Contains("já está em uso", response?.error?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateChannelById_WithDuplicateInstanceName_ReturnsBadRequest()
    {
        // Arrange
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        // Create two WhatsApp Não Oficial channels
        var canal1 = new Canal
        {
            FarmaciaIntegracosId = farmacia.Integracoes!.Id,
            Nome = "instance-one",
            Tipo = 1,
            IsOfficial = false,
            ApiUrl = "http://108.174.150.4:6000/api/whatsapp",
            ApiKey = "test-key-1"
        };
        var canal2 = new Canal
        {
            FarmaciaIntegracosId = farmacia.Integracoes!.Id,
            Nome = "instance-two",
            Tipo = 1,
            IsOfficial = false,
            ApiUrl = "http://108.174.150.4:6000/api/whatsapp",
            ApiKey = "test-key-2"
        };
        context.Canais.Add(canal1);
        context.Canais.Add(canal2);
        await context.SaveChangesAsync();

        var controller = CreateController(context, farmacia.Id);

        var updateRequest = new CreateChannelRequest(
            Name: "WhatsApp Not Official Updated",
            Type: 1,
            IsOfficial: false,
            AccessToken: null,
            PhoneNumberId: null,
            InstagramBusinessId: null,
            BusinessAccountId: null,
            ApiUrl: "http://108.174.150.4:6000/api/whatsapp",
            ApiKey: "test-api-key",
            InstanceName: " INSTANCE-ONE ",
            InstagramUsername: null,
            InstagramPassword: null); // Try to change canal2 to use canal1's name

        // Act
        var result = await controller.UpdateChannelById(canal2.Id, updateRequest);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var response = badRequest.Value as dynamic;
        Assert.NotNull(response?.error);
        Assert.Contains("já está em uso", response?.error?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
    }



    [Fact]
    public async Task UpdateChannel_CanChangeInstanceNameIfUnique_Succeeds()
    {
        // Arrange
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var canal = new Canal
        {
            FarmaciaIntegracosId = farmacia.Integracoes!.Id,
            Nome = "WhatsApp Not Official",
            Tipo = 1,
            IsOfficial = false,
            InstanceName = "old-instance",
            ApiUrl = "http://108.174.150.4:6000/api/whatsapp",
            ApiKey = "test-key"
        };
        context.Canais.Add(canal);
        await context.SaveChangesAsync();

        var controller = CreateController(context, farmacia.Id);

        var updateRequest = new CreateChannelRequest(
            Name: "WhatsApp Not Official",
            Type: 1,
            IsOfficial: false,
            AccessToken: null,
            PhoneNumberId: null,
            InstagramBusinessId: null,
            BusinessAccountId: null,
            ApiUrl: "http://108.174.150.4:6000/api/whatsapp",
            ApiKey: "test-api-key-updated",
            InstanceName: "new-unique-instance",
            InstagramUsername: null,
            InstagramPassword: null);

        // Act
        var result = await controller.UpdateChannel(1, false, updateRequest);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult);

        // Verify the channel name and instance name remain separated
        var updatedCanal = context.Canais.FirstOrDefault(c => c.Id == canal.Id);
        Assert.NotNull(updatedCanal);
        Assert.Equal("WhatsApp Not Official", updatedCanal.Nome);
        Assert.Equal("new-unique-instance", updatedCanal.InstanceName);

        // Verify FarmaciaIntegracoes was synced
        var updatedIntegracoes = context.FarmaciaIntegracoes.FirstOrDefault(i => i.Id == farmacia.Integracoes!.Id);
        Assert.NotNull(updatedIntegracoes);
        Assert.Equal("new-unique-instance", updatedIntegracoes.ChaveWhatsApp);
        Assert.Equal("http://108.174.150.4:6000/api/whatsapp", updatedIntegracoes.WhatsAppApiUrl);
        Assert.Equal("test-api-key-updated", updatedIntegracoes.WhatsAppApiKey);
    }

    [Fact]
    public async Task GetChannel_ForNonOfficialWhatsApp_ReturnsStoredChannelNameAndInstanceName()
    {
        // Arrange
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var canal = new Canal
        {
            FarmaciaIntegracosId = farmacia.Integracoes!.Id,
            Nome = "Canal da Loja",
            Tipo = 1,
            IsOfficial = false,
            InstanceName = "leandrokhalil",
            ApiUrl = "http://108.174.150.4:6000/api/whatsapp",
            ApiKey = "test-key"
        };
        context.Canais.Add(canal);
        await context.SaveChangesAsync();

        var controller = CreateController(context, farmacia.Id);

        // Act
        var result = await controller.GetChannel(1, false);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        var doc = System.Text.Json.JsonDocument.Parse(json);
        var channelName = doc.RootElement.GetProperty("data").GetProperty("name").GetString();
        var instanceName = doc.RootElement.GetProperty("data").GetProperty("instanceName").GetString();
        var hasPersistedApiKey = doc.RootElement.GetProperty("data").GetProperty("hasPersistedApiKey").GetBoolean();
        Assert.Equal("Canal da Loja", channelName);
        Assert.Equal("leandrokhalil", instanceName);
        Assert.True(hasPersistedApiKey);
    }

    [Fact]
    public async Task GetChannelById_ForNonOfficialWhatsApp_ReturnsPersistedApiKeyFlag()
    {
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var canal = new Canal
        {
            FarmaciaIntegracosId = farmacia.Integracoes!.Id,
            Nome = "Canal da Loja",
            Tipo = 1,
            IsOfficial = false,
            InstanceName = "leandrokhalil",
            ApiUrl = "http://108.174.150.4:6000/api/whatsapp",
            ApiKey = "test-key"
        };
        context.Canais.Add(canal);
        await context.SaveChangesAsync();

        var controller = CreateController(context, farmacia.Id);

        var result = await controller.GetChannelById(canal.Id);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal(1, doc.RootElement.GetProperty("data").GetProperty("type").GetInt32());
        Assert.False(doc.RootElement.GetProperty("data").GetProperty("isOfficial").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("data").GetProperty("hasPersistedApiKey").GetBoolean());
        Assert.Equal(string.Empty, doc.RootElement.GetProperty("data").GetProperty("apiUrl").GetString());
    }

    [Fact]
    public async Task GetChannelById_ForNonOfficialWhatsApp_UsesIntegracoesInstanceWhenCanalInstanceIsMissing()
    {
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        farmacia.Integracoes!.ChaveWhatsApp = "5562998808389";
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var canal = new Canal
        {
            FarmaciaIntegracosId = farmacia.Integracoes!.Id,
            Nome = "Canal sem instancia",
            Tipo = 1,
            IsOfficial = false,
            InstanceName = null,
            ApiUrl = "http://localhost:8080",
            ApiKey = "test-key"
        };
        context.Canais.Add(canal);
        await context.SaveChangesAsync();

        var controller = CreateController(context, farmacia.Id);
        var result = await controller.GetChannelById(canal.Id);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal("5562998808389", doc.RootElement.GetProperty("data").GetProperty("instanceName").GetString());
    }

    [Fact]
    public async Task CreateChannel_NonOfficialWhatsApp_SyncsFarmaciaIntegracoes()
    {
        // Arrange
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var controller = CreateController(context, farmacia.Id);

        var request = new CreateChannelRequest(
            Name: "WhatsApp Evolution",
            Type: 1,
            IsOfficial: false,
            AccessToken: null,
            PhoneNumberId: null,
            InstagramBusinessId: null,
            BusinessAccountId: null,
            ApiUrl: "http://108.174.150.4:6000/api/whatsapp",
            ApiKey: "my-api-key",
            InstanceName: "minha-instancia",
            InstagramUsername: null,
            InstagramPassword: null);

        // Act
        var result = await controller.CreateChannel(request);

        // Assert
        Assert.IsType<OkObjectResult>(result);

        var canal = context.Canais.Single();
        Assert.Equal("WhatsApp Evolution", canal.Nome);
        Assert.Equal("minha-instancia", canal.InstanceName);

        var integracoes = context.FarmaciaIntegracoes.FirstOrDefault(i => i.FarmaciaId == farmacia.Id);
        Assert.NotNull(integracoes);
        Assert.Equal("minha-instancia", integracoes.ChaveWhatsApp);
        Assert.Equal("http://108.174.150.4:6000/api/whatsapp", integracoes.WhatsAppApiUrl);
        Assert.Equal("my-api-key", integracoes.WhatsAppApiKey);
    }

    [Fact]
    public async Task CreateChannel_NonOfficialWhatsApp_UsesGlobalApiKeyWhenRequestOmitsIt()
    {
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var controller = CreateController(context, farmacia.Id);

        var request = new CreateChannelRequest(
            Name: "WhatsApp Evolution",
            Type: 1,
            IsOfficial: false,
            AccessToken: null,
            PhoneNumberId: null,
            InstagramBusinessId: null,
            BusinessAccountId: null,
            ApiUrl: "http://108.174.150.4:8080",
            ApiKey: null,
            InstanceName: "minha-instancia",
            InstagramUsername: null,
            InstagramPassword: null);

        var result = await controller.CreateChannel(request);

        Assert.IsType<OkObjectResult>(result);

        var integracoes = context.FarmaciaIntegracoes.FirstOrDefault(i => i.FarmaciaId == farmacia.Id);
        Assert.NotNull(integracoes);
        Assert.Equal("test-global-api-key", integracoes.WhatsAppApiKey);
    }

    [Fact]
    public async Task CreateChannel_NonOfficialWhatsApp_UsesPersistedApiKeyWhenRequestOmitsIt()
    {
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        farmacia.Integracoes!.WhatsAppApiKey = "stored-key";
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["GLOBAL_API_KEY"] = ""
        }).Build();
        var controller = CreateController(context, farmacia.Id, configuration: configuration);

        var request = new CreateChannelRequest(
            Name: "WhatsApp Evolution",
            Type: 1,
            IsOfficial: false,
            AccessToken: null,
            PhoneNumberId: null,
            InstagramBusinessId: null,
            BusinessAccountId: null,
            ApiUrl: "http://108.174.150.4:8080",
            ApiKey: null,
            InstanceName: "minha-instancia",
            InstagramUsername: null,
            InstagramPassword: null);

        var result = await controller.CreateChannel(request);

        Assert.IsType<OkObjectResult>(result);

        var canal = await context.Canais.SingleAsync();
        Assert.Equal("stored-key", canal.ApiKey);

        var integracoes = await context.FarmaciaIntegracoes.FirstAsync(i => i.FarmaciaId == farmacia.Id);
        Assert.Equal("stored-key", integracoes.WhatsAppApiKey);
    }

    [Fact]
    public async Task CreateChannel_NonOfficialWhatsApp_UsesConfiguredEvolutionUrlWhenRequestOmitsApiUrl()
    {
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["GLOBAL_API_KEY"] = "test-global-api-key",
            ["WhatsApp:NonOfficialApiUrl"] = "http://localhost:8080"
        }).Build();
        var controller = CreateController(context, farmacia.Id, configuration: configuration);

        var request = new CreateChannelRequest(
            Name: "WhatsApp Evolution",
            Type: 1,
            IsOfficial: false,
            AccessToken: null,
            PhoneNumberId: null,
            InstagramBusinessId: null,
            BusinessAccountId: null,
            ApiUrl: null,
            ApiKey: null,
            InstanceName: "minha-instancia",
            InstagramUsername: null,
            InstagramPassword: null);

        var result = await controller.CreateChannel(request);

        Assert.IsType<OkObjectResult>(result);
        var canal = await context.Canais.SingleAsync();
        Assert.Equal("http://localhost:8080", canal.ApiUrl);

        var integracoes = await context.FarmaciaIntegracoes.FirstAsync(i => i.FarmaciaId == farmacia.Id);
        Assert.Equal("http://localhost:8080", integracoes.WhatsAppApiUrl);
    }

    [Fact]
    public async Task CreateChannel_NonOfficialWhatsApp_WithEvolutionRootUrl_PreservesBaseUrlWithoutWebhookSuffix()
    {
        // Arrange
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var controller = CreateController(context, farmacia.Id);

        var request = new CreateChannelRequest(
            Name: "WhatsApp Evolution Root",
            Type: 1,
            IsOfficial: false,
            AccessToken: null,
            PhoneNumberId: null,
            InstagramBusinessId: null,
            BusinessAccountId: null,
            ApiUrl: "http://108.174.150.4:8080",
            ApiKey: "root-api-key",
            InstanceName: "root-instance",
            InstagramUsername: null,
            InstagramPassword: null);

        // Act
        var result = await controller.CreateChannel(request);

        // Assert
        Assert.IsType<OkObjectResult>(result);

        var integracoes = context.FarmaciaIntegracoes.FirstOrDefault(i => i.FarmaciaId == farmacia.Id);
        Assert.NotNull(integracoes);
        Assert.Equal("http://108.174.150.4:8080", integracoes.WhatsAppApiUrl);
        Assert.DoesNotContain("/webhook", integracoes.WhatsAppApiUrl);
    }

    [Fact]
    public async Task GenerateQrCode_WithMissingInstanceName_ReturnsBadRequest()
    {
        // Arrange
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var controller = CreateController(context, farmacia.Id);

        var request = new QrCodeGenerateRequest(
            InstanceName: "",
            ApiUrl: "http://host:6000/api/whatsapp",
            ApiKey: "test-key");

        // Act
        var result = await controller.GenerateQrCode(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetWhatsAppInstanceInfo_WithMissingParameters_ReturnsBadRequest()
    {
        // Arrange
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var controller = CreateController(context, farmacia.Id);

        // Act
        var result = await controller.GetWhatsAppInstanceInfo("", "http://host:6000/api/whatsapp", "key");

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UpdateChannelById_NonOfficialWhatsApp_UsesPersistedApiKeyWhenRequestOmitsIt()
    {
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var canal = new Canal
        {
            FarmaciaIntegracosId = farmacia.Integracoes!.Id,
            Nome = "WhatsApp Evo",
            Tipo = 1,
            IsOfficial = false,
            InstanceName = "inst-1",
            ApiUrl = "http://old-host:8080",
            ApiKey = "old-key"
        };
        context.Canais.Add(canal);
        await context.SaveChangesAsync();

        var controller = CreateController(context, farmacia.Id);

        var updateRequest = new CreateChannelRequest(
            Name: "WhatsApp Evo Updated",
            Type: 1,
            IsOfficial: false,
            AccessToken: null,
            PhoneNumberId: null,
            InstagramBusinessId: null,
            BusinessAccountId: null,
            ApiUrl: "http://new-host:8080",
            ApiKey: null, // omit; should keep the persisted channel key
            InstanceName: "inst-1",
            InstagramUsername: null,
            InstagramPassword: null,
            HasPersistedApiKey: true);

        var result = await controller.UpdateChannelById(canal.Id, updateRequest);

        Assert.IsType<OkObjectResult>(result);
        var updated = await context.Canais.FirstAsync(c => c.Id == canal.Id);
        Assert.Equal("old-key", updated.ApiKey);
        Assert.Equal("http://new-host:8080", updated.ApiUrl);
        var updatedIntegracoes = await context.FarmaciaIntegracoes.FirstAsync(i => i.Id == farmacia.Integracoes!.Id);
        Assert.Equal("old-key", updatedIntegracoes.WhatsAppApiKey);
    }

    [Fact]
    public async Task GetWhatsAppInstanceInfo_WithInvalidApiUrl_ReturnsBadRequest()
    {
        // Arrange
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var controller = CreateController(context, farmacia.Id);

        // Act
        var result = await controller.GetWhatsAppInstanceInfo("test-instance", "not-a-url", "key");

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task EnsureWhatsAppInstance_WithMissingInstanceName_ReturnsBadRequest()
    {
        // Arrange
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var controller = CreateController(context, farmacia.Id);

        var request = new QrCodeGenerateRequest(
            InstanceName: "",
            ApiUrl: "http://host:6000/api/whatsapp",
            ApiKey: "test-key");

        // Act
        var result = await controller.EnsureWhatsAppInstance(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task EnsureWhatsAppInstance_WithInvalidApiUrl_ReturnsBadRequest()
    {
        // Arrange
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var controller = CreateController(context, farmacia.Id);

        var request = new QrCodeGenerateRequest(
            InstanceName: "test-instance",
            ApiUrl: "not-valid",
            ApiKey: "test-key");

        // Act
        var result = await controller.EnsureWhatsAppInstance(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task EnsureWhatsAppInstance_WhenCreatingInstance_UsesTypedIdentifierAsIdentityAndGeneratesUniqueToken()
    {
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var handler = new QueueHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]")
            },
            new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("""{"instance":{"instanceName":"5511999999999"}}""")
            });

        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var controller = CreateController(context, farmacia.Id, factory.Object);
        var request = new QrCodeGenerateRequest(
            InstanceName: " 5511999999999 ",
            ApiUrl: "http://localhost:8080",
            ApiKey: "master-key",
            Name: "Farmácia Matriz");

        var result = await controller.EnsureWhatsAppInstance(request);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("http://localhost:8080/instance/create", handler.RequestUris[1]);
        using var createDoc = System.Text.Json.JsonDocument.Parse(handler.RequestBodies[1]!);
        Assert.Equal("5511999999999", createDoc.RootElement.GetProperty("instanceName").GetString());
        Assert.Equal("5511999999999", createDoc.RootElement.GetProperty("name").GetString());
        Assert.NotEqual("master-key", createDoc.RootElement.GetProperty("token").GetString());
    }

    [Fact]
    public async Task EnsureWhatsAppInstance_WhenCreateReturnsDuplicateToken_RetriesWithNewToken()
    {
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var handler = new QueueHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]")
            },
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("""{"error":"ERROR: duplicate key value violates unique constraint \"uni_instances_token\" (SQLSTATE 23505)"}""")
            },
            new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("""{"instance":{"instanceName":"5511888888888"}}""")
            });

        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var controller = CreateController(context, farmacia.Id, factory.Object);
        var request = new QrCodeGenerateRequest(
            InstanceName: "5511888888888",
            ApiUrl: "http://localhost:8080",
            ApiKey: "master-key",
            Name: "Farmácia Matriz");

        var result = await controller.EnsureWhatsAppInstance(request);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(3, handler.RequestUris.Count);
        Assert.Equal("http://localhost:8080/instance/create", handler.RequestUris[1]);
        Assert.Equal("http://localhost:8080/instance/create", handler.RequestUris[2]);

        using var firstCreateDoc = System.Text.Json.JsonDocument.Parse(handler.RequestBodies[1]!);
        using var secondCreateDoc = System.Text.Json.JsonDocument.Parse(handler.RequestBodies[2]!);
        Assert.Equal("5511888888888", firstCreateDoc.RootElement.GetProperty("instanceName").GetString());
        Assert.Equal("5511888888888", secondCreateDoc.RootElement.GetProperty("instanceName").GetString());
        Assert.NotEqual(
            firstCreateDoc.RootElement.GetProperty("token").GetString(),
            secondCreateDoc.RootElement.GetProperty("token").GetString());
        Assert.Equal("5511888888888", firstCreateDoc.RootElement.GetProperty("name").GetString());
        Assert.Equal("5511888888888", secondCreateDoc.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task EnsureWhatsAppInstance_WhenCreateReturnsAlreadyExists_TreatsAsSuccess()
    {
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var handler = new QueueHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]")
            },
            new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent("""{"error":"instance already exists"}""")
            });

        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var controller = CreateController(context, farmacia.Id, factory.Object);
        var request = new QrCodeGenerateRequest(
            InstanceName: "5511777777777",
            ApiUrl: "http://localhost:8080",
            ApiKey: "master-key");

        var result = await controller.EnsureWhatsAppInstance(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("created").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("alreadyExists").GetBoolean());
    }

    [Fact]
    public async Task EnsureWhatsAppInstance_WithForceRecreate_DeletesBeforeCreating()
    {
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var handler = new QueueHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{"name":"5511666666666","token":"master-key","connectionStatus":"close"}]""")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"success":true}""")
            },
            new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("""{"instance":{"instanceName":"5511666666666"}}""")
            });

        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var controller = CreateController(context, farmacia.Id, factory.Object);
        var request = new QrCodeGenerateRequest(
            InstanceName: "5511666666666",
            ApiUrl: "http://localhost:8080",
            ApiKey: "master-key",
            ForceRecreate: true);

        var result = await controller.EnsureWhatsAppInstance(request);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("http://localhost:8080/instance/fetchInstances?instanceName=5511666666666", handler.RequestUris[0]);
        Assert.Equal("http://localhost:8080/instance/delete/5511666666666", handler.RequestUris[1]);
        Assert.Equal("http://localhost:8080/instance/create", handler.RequestUris[2]);
    }

    [Fact]
    public async Task GenerateQrCode_UsesEvolutionGoQrcodeEndpointAndReturnsNestedBase64()
    {
        // Quando a instância já existe na Evolution Go, o endpoint NÃO deve chamar
        // POST /instance/create, pois isso pode redefinir o estado de QR Code.
        // Deve ir diretamente ao fetchInstances (para resolver o token) e então ao endpoint de QR.
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var handler = new QueueHttpMessageHandler(
            // 1. fetchInstances — instância já existe, retorna token
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{"name":"test-instance","token":"instance-token","connectionStatus":"close"}]""")
            },
            // 2. GET /instance/test-instance/qrcode → QR Code disponível
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"qrcode":{"base64":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"}}""")
            });

        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var controller = CreateController(context, farmacia.Id, factory.Object);
        var request = new QrCodeGenerateRequest("test-instance", "http://localhost:8080", "master-key");

        var result = await controller.GenerateQrCode(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", doc.RootElement.GetProperty("base64").GetString());
        // Instância existia — NÃO deve ter chamado POST /instance/create
        Assert.DoesNotContain(handler.RequestUris, uri => uri.Contains("/instance/create", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("http://localhost:8080/instance/fetchInstances?instanceName=test-instance", handler.RequestUris[0]);
        Assert.Equal("http://localhost:8080/instance/test-instance/qrcode", handler.RequestUris[1]);
        Assert.Equal("instance-token", handler.ApiKeys[1]);
    }

    [Fact]
    public async Task GenerateQrCode_WhenInstanceDoesNotExist_CreatesInstanceThenFetchesQrCode()
    {
        // Quando a instância NÃO existe (fetchInstances retorna vazio), deve criar e então
        // buscar o QR Code. Isso evita que a criação seja feita cegamente em todos os retries.
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var handler = new QueueHttpMessageHandler(
            // 1. fetchInstances (primeira tentativa — instância não existe)
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]")
            },
            // 2. POST /instance/create
            new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("""{"instance":{"instanceName":"test-instance"}}""")
            },
            // 3. fetchInstances (após criar — retorna token da instância)
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{"name":"test-instance","token":"instance-token","connectionStatus":"close"}]""")
            },
            // 4. GET /instance/test-instance/qrcode → QR Code disponível
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"base64":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"}""")
            });

        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var controller = CreateController(context, farmacia.Id, factory.Object);
        var request = new QrCodeGenerateRequest("test-instance", "http://localhost:8080", "master-key");

        var result = await controller.GenerateQrCode(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("http://localhost:8080/instance/fetchInstances?instanceName=test-instance", handler.RequestUris[0]);
        Assert.Equal("http://localhost:8080/instance/create", handler.RequestUris[1]);
        Assert.Equal("http://localhost:8080/instance/fetchInstances?instanceName=test-instance", handler.RequestUris[2]);
        Assert.Equal("http://localhost:8080/instance/test-instance/qrcode", handler.RequestUris[3]);
        Assert.Equal("instance-token", handler.ApiKeys[3]);
        using var createDoc = System.Text.Json.JsonDocument.Parse(handler.RequestBodies[1]!);
        Assert.NotEqual("master-key", createDoc.RootElement.GetProperty("token").GetString());
        Assert.Equal("test-instance", createDoc.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task GenerateQrCode_WhenCreatingNumericInstance_UsesUserIdentifierWithoutLegacyPrefix()
    {
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var handler = new QueueHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]")
            },
            new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("""{"instance":{"instanceName":"5562998808389"}}""")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{"name":"5562998808389","token":"instance-token","connectionStatus":"close"}]""")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"base64":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"}""")
            });

        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var controller = CreateController(context, farmacia.Id, factory.Object);

        var result = await controller.GenerateQrCode(new QrCodeGenerateRequest("5562998808389", "http://localhost:8080", "master-key"));

        Assert.IsType<OkObjectResult>(result);
        using var createDoc = System.Text.Json.JsonDocument.Parse(handler.RequestBodies[1]!);
        Assert.Equal("5562998808389", createDoc.RootElement.GetProperty("instanceName").GetString());
        Assert.Equal("5562998808389", createDoc.RootElement.GetProperty("name").GetString());
        Assert.NotEqual("master-key", createDoc.RootElement.GetProperty("token").GetString());
    }

    [Fact]
    public async Task GenerateQrCode_WhenCreateFails_ReturnsCreateEndpointStatusAndSkipsQrRequests()
    {
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var handler = new QueueHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]")
            },
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("unauthorized")
            });

        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var controller = CreateController(context, farmacia.Id, factory.Object);
        var result = await controller.GenerateQrCode(new QrCodeGenerateRequest("5562998808389", "http://localhost:8080", "master-key"));

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, objectResult.StatusCode);
        var json = System.Text.Json.JsonSerializer.Serialize(objectResult.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var error = doc.RootElement.GetProperty("error").GetString();
        Assert.Contains("POST /instance/create", error);
        Assert.Contains("401", error);
        Assert.Contains("unauthorized", error);
        Assert.Equal(2, handler.RequestUris.Count);
    }

    [Fact]
    public async Task GenerateQrCode_WhenCreateSucceedsButInstanceDoesNotAppear_ReturnsExplicitCreateFailure()
    {
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var handler = new QueueHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]")
            },
            new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("""{"instance":{"instanceName":"5562998808389"}}""")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]")
            });

        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var controller = CreateController(context, farmacia.Id, factory.Object);
        var result = await controller.GenerateQrCode(new QrCodeGenerateRequest("5562998808389", "http://localhost:8080", "master-key"));

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status502BadGateway, objectResult.StatusCode);
        var json = System.Text.Json.JsonSerializer.Serialize(objectResult.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var error = doc.RootElement.GetProperty("error").GetString();
        Assert.Contains("não encontrada após POST /instance/create", error);
        Assert.Equal(3, handler.RequestUris.Count);
    }

    [Fact]
    public async Task GenerateQrCode_WhenCreateReturns400_RetriesWithMinimalPayload()
    {
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var handler = new QueueHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]")
            },
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("""{"error":"invalid payload"}""")
            },
            new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("""{"instance":{"instanceName":"5562998808389"}}""")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{"name":"5562998808389","token":"instance-token","connectionStatus":"close"}]""")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"base64":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"}""")
            });

        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var controller = CreateController(context, farmacia.Id, factory.Object);
        var result = await controller.GenerateQrCode(new QrCodeGenerateRequest("5562998808389", "http://localhost:8080", "master-key", Name: "Farmácia Matriz"));

        Assert.IsType<OkObjectResult>(result);
        using var firstCreateDoc = System.Text.Json.JsonDocument.Parse(handler.RequestBodies[1]!);
        Assert.True(firstCreateDoc.RootElement.TryGetProperty("token", out _));
        Assert.Equal("5562998808389", firstCreateDoc.RootElement.GetProperty("name").GetString());

        using var secondCreateDoc = System.Text.Json.JsonDocument.Parse(handler.RequestBodies[2]!);
        Assert.Equal("5562998808389", secondCreateDoc.RootElement.GetProperty("instanceName").GetString());
        Assert.Equal("WHATSAPP-BAILEYS", secondCreateDoc.RootElement.GetProperty("integration").GetString());
        Assert.False(secondCreateDoc.RootElement.TryGetProperty("token", out _));
        Assert.Equal("5562998808389", secondCreateDoc.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task GenerateQrCode_WhenCreateReturnsDuplicateToken_RetriesOnceWithNewTokenThenFetchesQrCode()
    {
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var handler = new QueueHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]")
            },
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("""{"error":"ERROR: duplicate key value violates unique constraint \"uni_instances_token\" (SQLSTATE 23505)"}""")
            },
            new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("""{"instance":{"instanceName":"5562998808389"}}""")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{"name":"5562998808389","token":"instance-token","connectionStatus":"close"}]""")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"base64":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"}""")
            });

        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var controller = CreateController(context, farmacia.Id, factory.Object);
        var result = await controller.GenerateQrCode(new QrCodeGenerateRequest("5562998808389", "http://localhost:8080", "master-key", Name: "Farmácia Matriz"));

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(5, handler.RequestUris.Count);
        Assert.Equal("http://localhost:8080/instance/create", handler.RequestUris[1]);
        Assert.Equal("http://localhost:8080/instance/create", handler.RequestUris[2]);
        Assert.Equal("http://localhost:8080/instance/fetchInstances?instanceName=5562998808389", handler.RequestUris[3]);
        Assert.Equal("http://localhost:8080/instance/5562998808389/qrcode", handler.RequestUris[4]);

        using var firstCreateDoc = System.Text.Json.JsonDocument.Parse(handler.RequestBodies[1]!);
        using var secondCreateDoc = System.Text.Json.JsonDocument.Parse(handler.RequestBodies[2]!);
        Assert.Equal("5562998808389", firstCreateDoc.RootElement.GetProperty("instanceName").GetString());
        Assert.Equal("5562998808389", secondCreateDoc.RootElement.GetProperty("instanceName").GetString());
        Assert.NotEqual(
            firstCreateDoc.RootElement.GetProperty("token").GetString(),
            secondCreateDoc.RootElement.GetProperty("token").GetString());
        Assert.Equal("5562998808389", firstCreateDoc.RootElement.GetProperty("name").GetString());
        Assert.Equal("5562998808389", secondCreateDoc.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task GenerateQrCode_WhenDuplicateTokenRetryFails_ReturnsExplicitCreateFailure()
    {
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var handler = new QueueHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]")
            },
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("""{"error":"ERROR: duplicate key value violates unique constraint \"uni_instances_token\" (SQLSTATE 23505)"}""")
            },
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("""{"error":"ERROR: duplicate key value violates unique constraint \"uni_instances_token\" (SQLSTATE 23505)"}""")
            });

        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var controller = CreateController(context, farmacia.Id, factory.Object);
        var result = await controller.GenerateQrCode(new QrCodeGenerateRequest("5562998808389", "http://localhost:8080", "master-key"));

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
        var json = System.Text.Json.JsonSerializer.Serialize(objectResult.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var error = doc.RootElement.GetProperty("error").GetString();
        Assert.Contains("POST /instance/create", error);
        Assert.Contains("500", error);
        Assert.Contains("uni_instances_token", error);
        Assert.Equal(3, handler.RequestUris.Count);

        using var firstCreateDoc = System.Text.Json.JsonDocument.Parse(handler.RequestBodies[1]!);
        using var secondCreateDoc = System.Text.Json.JsonDocument.Parse(handler.RequestBodies[2]!);
        Assert.NotEqual(
            firstCreateDoc.RootElement.GetProperty("token").GetString(),
            secondCreateDoc.RootElement.GetProperty("token").GetString());
    }

    [Fact]
    public async Task GenerateQrCode_WhenFetchInstancesReturnsQrCode_SavesImageAndReturnsImageUrl()
    {
        var tempWebRoot = Path.Combine(Path.GetTempPath(), $"pharmazap-qr-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempWebRoot);

        try
        {
            var context = CreateDbContext();
            var farmacia = CreateTestFarmacia();
            context.Farmacias.Add(farmacia);
            await context.SaveChangesAsync();

            var pngBase64 = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/nZkAAAAASUVORK5CYII=";
            var handler = new QueueHttpMessageHandler(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"[{{\"name\":\"test-instance\",\"token\":\"instance-token\",\"connectionStatus\":\"qrcode\",\"qrcode\":{{\"base64\":\"{pngBase64}\"}}}}]")
                });

            var httpClient = new HttpClient(handler);
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var controller = CreateController(
                context,
                farmacia.Id,
                factory.Object,
                webHostEnvironment: CreateWebHostEnvironment(tempWebRoot));
            var request = new QrCodeGenerateRequest("test-instance", "http://localhost:8080/api/whatsapp/", "master-key");

            var result = await controller.GenerateQrCode(request);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
            var imageUrl = doc.RootElement.GetProperty("imageUrl").GetString();
            Assert.NotNull(imageUrl);
            Assert.True(File.Exists(Path.Combine(tempWebRoot, imageUrl!.TrimStart('/').Replace('/', Path.DirectorySeparatorChar))));
            Assert.Single(handler.RequestUris);
            Assert.Equal("http://localhost:8080/instance/fetchInstances?instanceName=test-instance", handler.RequestUris[0]);
        }
        finally
        {
            if (Directory.Exists(tempWebRoot))
                Directory.Delete(tempWebRoot, true);
        }
    }

    [Fact]
    public async Task GenerateQrCode_WhenPrimaryQrcodeRouteFails_FallsBackToEvolutionGoQrcodeRoute()
    {
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var handler = new QueueHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{"name":"test-instance","token":"instance-token","connectionStatus":"close"}]""")
            },
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("404 page not found")
            },
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("404 page not found")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"qrcode":{"base64":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"}}""")
            });

        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var controller = CreateController(context, farmacia.Id, factory.Object);
        var request = new QrCodeGenerateRequest("test-instance", "http://localhost:8080", "master-key");

        var result = await controller.GenerateQrCode(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.DoesNotContain(handler.RequestUris, uri => uri.Contains("/instance/create", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("http://localhost:8080/instance/fetchInstances?instanceName=test-instance", handler.RequestUris[0]);
        Assert.Equal("http://localhost:8080/instance/test-instance/qrcode", handler.RequestUris[1]);
        Assert.Equal("instance-token", handler.ApiKeys[1]);
        Assert.Equal("http://localhost:8080/instance/test-instance/qrcode", handler.RequestUris[2]);
        Assert.Equal("master-key", handler.ApiKeys[2]);
        Assert.Equal("http://localhost:8080/instance/qrcode/test-instance", handler.RequestUris[3]);
        Assert.Equal("instance-token", handler.ApiKeys[3]);
    }

    [Fact]
    public async Task GenerateQrCode_WhenEvolutionGoQrcodeEndpointFails_FallsBackToLegacyConnectEndpoint()
    {
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var handler = new QueueHttpMessageHandler(
            // 1. fetchInstances — instância existe
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{"name":"test-instance","token":"instance-token","connectionStatus":"close"}]""")
            },
            // 2. GET /instance/{name}/qrcode com instance-token — falha
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("""{"message":"not found"}""")
            },
            // 3. Retry do mesmo endpoint com master-key
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("""{"message":"not found"}""")
            },
            // 4. Fallback Evolution Go: GET /instance/qrcode/{name} com instance-token — falha
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("""{"message":"not found"}""")
            },
            // 5. Retry do fallback Evolution Go com master-key
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("""{"message":"not found"}""")
            },
            // 6. Fallback adicional: GET /instance/{name}/qr com instance-token — falha
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("""{"message":"not found"}""")
            },
            // 7. Retry do fallback adicional com master-key
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("""{"message":"not found"}""")
            },
            // 8. Fallback legado: GET /instance/connect/{name} — retorna QR
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"qrcode":{"base64":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"}}""")
            });

        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var controller = CreateController(context, farmacia.Id, factory.Object);
        var request = new QrCodeGenerateRequest("test-instance", "http://localhost:8080", "master-key");

        var result = await controller.GenerateQrCode(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        // Instância existia — NÃO deve ter chamado POST /instance/create
        Assert.DoesNotContain(handler.RequestUris, uri => uri.Contains("/instance/create", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("http://localhost:8080/instance/fetchInstances?instanceName=test-instance", handler.RequestUris[0]);
        Assert.Equal("http://localhost:8080/instance/test-instance/qrcode", handler.RequestUris[1]);
        Assert.Equal("instance-token", handler.ApiKeys[1]);
        Assert.Equal("http://localhost:8080/instance/test-instance/qrcode", handler.RequestUris[2]);
        Assert.Equal("master-key", handler.ApiKeys[2]);
        Assert.Equal("http://localhost:8080/instance/qrcode/test-instance", handler.RequestUris[3]);
        Assert.Equal("instance-token", handler.ApiKeys[3]);
        Assert.Equal("http://localhost:8080/instance/qrcode/test-instance", handler.RequestUris[4]);
        Assert.Equal("master-key", handler.ApiKeys[4]);
        Assert.Equal("http://localhost:8080/instance/test-instance/qr", handler.RequestUris[5]);
        Assert.Equal("instance-token", handler.ApiKeys[5]);
        Assert.Equal("http://localhost:8080/instance/test-instance/qr", handler.RequestUris[6]);
        Assert.Equal("master-key", handler.ApiKeys[6]);
        Assert.Equal("http://localhost:8080/instance/connect/test-instance", handler.RequestUris[7]);
        Assert.Equal("instance-token", handler.ApiKeys[7]);
    }

    [Fact]
    public async Task GenerateQrCode_WhenAllQrEndpointsReturn404_ReturnsLastMethodAndPathInError()
    {
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var handler = new QueueHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{"name":"test-instance","token":"master-key","connectionStatus":"close"}]""")
            },
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("404 page not found")
            },
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("404 page not found")
            },
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("404 page not found")
            },
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("404 page not found")
            },
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("404 page not found")
            },
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("404 page not found")
            });

        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var controller = CreateController(context, farmacia.Id, factory.Object);
        var request = new QrCodeGenerateRequest("test-instance", "http://localhost:8080/", "master-key");

        var result = await controller.GenerateQrCode(request);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
        var json = System.Text.Json.JsonSerializer.Serialize(objectResult.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var error = doc.RootElement.GetProperty("error").GetString();
        Assert.Contains("GET /instance/qr", error);
        Assert.Contains("404 page not found", error);
        Assert.Equal("http://localhost:8080/instance/fetchInstances?instanceName=test-instance", handler.RequestUris[0]);
        Assert.Equal("http://localhost:8080/instance/test-instance/qrcode", handler.RequestUris[1]);
        Assert.Equal("http://localhost:8080/instance/qrcode/test-instance", handler.RequestUris[2]);
        Assert.Equal("http://localhost:8080/instance/test-instance/qr", handler.RequestUris[3]);
        Assert.Equal("http://localhost:8080/instance/connect/test-instance", handler.RequestUris[4]);
        Assert.Equal("http://localhost:8080/instance/qr?instanceName=test-instance", handler.RequestUris[5]);
        Assert.Equal("http://localhost:8080/instance/qr", handler.RequestUris[6]);
    }

    [Fact]
    public async Task GenerateQrCode_WithoutRequestOrPersistedApiKey_FallsBackToGlobalApiKey()
    {
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var handler = new QueueHttpMessageHandler(
            // 1. fetchInstances com chave global — instância existe
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{"name":"test-instance","token":"instance-token","connectionStatus":"close"}]""")
            },
            // 2. GET /instance/{name}/qrcode → QR disponível
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"qrcode":{"base64":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"}}""")
            });

        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var controller = CreateController(context, farmacia.Id, factory.Object);
        var request = new QrCodeGenerateRequest("test-instance", "http://localhost:8080", ApiKey: null!);

        var result = await controller.GenerateQrCode(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", doc.RootElement.GetProperty("base64").GetString());
        // fetchInstances deve usar a chave global como fallback
        Assert.Equal("test-global-api-key", handler.ApiKeys[0]);

        var integracoes = await context.FarmaciaIntegracoes
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.FarmaciaId == farmacia.Id);
        Assert.NotNull(integracoes);
        Assert.Equal("test-global-api-key", integracoes!.WhatsAppApiKey);
        Assert.Equal("http://localhost:8080", integracoes.WhatsAppApiUrl);
        Assert.Equal("test-instance", integracoes.ChaveWhatsApp);
    }

    // Regression test for issue #443: quando a Evolution devolve JSON sem QR Code pronto
    // (ex.: {"pairingCode":null,"code":null,"count":0}), o controller NÃO pode responder
    // {success:true, base64:"<JSON completo>"} — isso faz o frontend renderizar
    // "data:image/png;base64,{JSON}" como imagem quebrada. Deve retornar 202 para permitir
    // que a lógica de retry do frontend tente novamente.
    [Fact]
    public async Task GenerateQrCode_WhenEvolutionReturnsJsonWithoutBase64_Returns202Accepted()
    {
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var handler = new QueueHttpMessageHandler(
            // fetchInstances — instância existe
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{"name":"test-instance","token":"instance-token","connectionStatus":"close"}]""")
            },
            // GET /instance/{name}/qrcode — resposta sem QR (ainda não disponível)
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"pairingCode":null,"code":null,"count":0}""")
            },
            // Retry com master-key — mesma resposta sem QR
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"pairingCode":null,"code":null,"count":0}""")
            },
            // Fallback connect — também sem QR
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"pairingCode":null,"code":null,"count":0}""")
            });

        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var controller = CreateController(context, farmacia.Id, factory.Object);
        var request = new QrCodeGenerateRequest("test-instance", "http://localhost:8080", "master-key");

        var result = await controller.GenerateQrCode(request);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status202Accepted, statusResult.StatusCode);
        var json = System.Text.Json.JsonSerializer.Serialize(statusResult.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        // Não pode devolver o JSON como se fosse base64
        Assert.False(doc.RootElement.TryGetProperty("base64", out _));
    }

    // Regression test for issue #443: strings curtas (ex.: 6 chars) não são QR Codes
    // válidos e não devem ser aceitas — evita "success" falso e imagem quebrada.
    [Fact]
    public async Task GenerateQrCode_WhenExtractedBase64IsTooShort_Returns202Accepted()
    {
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var handler = new QueueHttpMessageHandler(
            // fetchInstances — instância existe
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{"name":"test-instance","token":"instance-token","connectionStatus":"close"}]""")
            },
            // GET /instance/{name}/qrcode — base64 vazio/curto
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"base64":""}""")
            },
            // Retry com master-key
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"base64":""}""")
            },
            // Fallback connect
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"base64":""}""")
            });

        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var controller = CreateController(context, farmacia.Id, factory.Object);
        var request = new QrCodeGenerateRequest("test-instance", "http://localhost:8080", "master-key");

        var result = await controller.GenerateQrCode(request);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status202Accepted, statusResult.StatusCode);
    }

    // A Evolution Go pode retornar o base64 puro em texto (não JSON). Nesse caso o
    // fallback continua sendo válido, desde que o payload seja plausível (>= 100 chars).
    [Fact]
    public async Task GenerateQrCode_WhenEvolutionReturnsRawBase64Body_ReturnsSuccess()
    {
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var rawBase64 = new string('B', 250);
        var handler = new QueueHttpMessageHandler(
            // fetchInstances — instância existe
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{"name":"test-instance","token":"instance-token","connectionStatus":"close"}]""")
            },
            // GET /instance/{name}/qrcode — base64 puro (não JSON)
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(rawBase64)
            });

        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var controller = CreateController(context, farmacia.Id, factory.Object);
        var request = new QrCodeGenerateRequest("test-instance", "http://localhost:8080", "master-key");

        var result = await controller.GenerateQrCode(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(rawBase64, doc.RootElement.GetProperty("base64").GetString());
    }

    [Fact]
    public async Task GenerateQrCode_WhenEvolutionReturnsDataUrlBase64_ReturnsSuccessWithDataUrl()
    {
        // A Evolution Go retorna o QR Code como data URL completa (data:image/png;base64,...).
        // O backend deve retorná-la intacta ao frontend sem adicionar prefixo redundante.
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var dataUrl = "data:image/png;base64," + new string('A', 200);
        var handler = new QueueHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{"name":"test-instance","token":"instance-token","connectionStatus":"close"}]""")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($@"{{""base64"":""{dataUrl}""}}")
            });

        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var controller = CreateController(context, farmacia.Id, factory.Object);
        var request = new QrCodeGenerateRequest("test-instance", "http://localhost:8080", "master-key");

        var result = await controller.GenerateQrCode(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(dataUrl, doc.RootElement.GetProperty("base64").GetString());
        Assert.True(doc.RootElement.GetProperty("imageUrl").GetString()?.StartsWith("/uploads/qrcodes/", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    [Fact]
    public async Task GenerateQrCode_WhenEvolutionReturnsDataUrl_SavesImageAndReturnsImageUrl()
    {
        var tempWebRoot = Path.Combine(Path.GetTempPath(), $"pharmazap-qr-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempWebRoot);

        try
        {
            var context = CreateDbContext();
            var farmacia = CreateTestFarmacia();
            context.Farmacias.Add(farmacia);
            await context.SaveChangesAsync();

            var pngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/nZkAAAAASUVORK5CYII=";
            var dataUrl = $"data:image/png;base64,{pngBase64}";
            var handler = new QueueHttpMessageHandler(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""[{"name":"test-instance","token":"instance-token","connectionStatus":"close"}]""")
                },
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($@"{{""base64"":""{dataUrl}""}}")
                });

            var httpClient = new HttpClient(handler);
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var controller = CreateController(context, farmacia.Id, factory.Object, webHostEnvironment: CreateWebHostEnvironment(tempWebRoot));
            var request = new QrCodeGenerateRequest("test-instance", "http://localhost:8080", "master-key");

            var result = await controller.GenerateQrCode(request);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
            var imageUrl = doc.RootElement.GetProperty("imageUrl").GetString();
            Assert.NotNull(imageUrl);
            Assert.Contains("/uploads/qrcodes/", imageUrl, StringComparison.OrdinalIgnoreCase);

            var relativePath = imageUrl!.TrimStart('/');
            var filePath = Path.Combine(tempWebRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(filePath));

            var bytes = await File.ReadAllBytesAsync(filePath);
            Assert.NotEmpty(bytes);
        }
        finally
        {
            if (Directory.Exists(tempWebRoot))
                Directory.Delete(tempWebRoot, true);
        }
    }

    [Fact]
    public async Task GenerateQrCode_WhenEvolutionReturnsTextualQrPayload_RendersAndSavesPng()
    {
        var tempWebRoot = Path.Combine(Path.GetTempPath(), $"pharmazap-qr-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempWebRoot);

        try
        {
            var context = CreateDbContext();
            var farmacia = CreateTestFarmacia();
            context.Farmacias.Add(farmacia);
            await context.SaveChangesAsync();

            var textualQrPayload = "2@textual-evolution-qr-payload-" + new string('x', 100);
            var handler = new QueueHttpMessageHandler(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""[{"name":"test-instance","token":"instance-token","connectionStatus":"close"}]""")
                },
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(textualQrPayload)
                });

            var httpClient = new HttpClient(handler);
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);
            var controller = CreateController(
                context,
                farmacia.Id,
                factory.Object,
                webHostEnvironment: CreateWebHostEnvironment(tempWebRoot));

            var result = await controller.GenerateQrCode(
                new QrCodeGenerateRequest("test-instance", "http://localhost:8080", "master-key"));

            var okResult = Assert.IsType<OkObjectResult>(result);
            var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var imageUrl = doc.RootElement.GetProperty("imageUrl").GetString();
            Assert.NotNull(imageUrl);

            var imagePath = Path.Combine(
                tempWebRoot,
                imageUrl!.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            var bytes = await File.ReadAllBytesAsync(imagePath);
            Assert.True(bytes.AsSpan().StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47 }));
        }
        finally
        {
            if (Directory.Exists(tempWebRoot))
                Directory.Delete(tempWebRoot, true);
        }
    }

    [Fact]
    public async Task GeneratePairingCode_UsesOfficialPairEndpointAndReadsNestedPascalCaseCode()
    {
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var handler = new QueueHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{"name":"test-instance","token":"instance-token","connectionStatus":"close"}]""")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"message":"success"}""")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"message":"success","data":{"PairingCode":"ABCD-1234"}}""")
            });

        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);
        var controller = CreateController(context, farmacia.Id, factory.Object);

        var result = await controller.GeneratePairingCode(
            new PairingCodeGenerateRequest("test-instance", "http://localhost:8080", "master-key", "5562998808389"));

        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("ABCD-1234", doc.RootElement.GetProperty("code").GetString());
        Assert.Equal("http://localhost:8080/instance/fetchInstances?instanceName=test-instance", handler.RequestUris[0]);
        Assert.Equal("http://localhost:8080/instance/connect", handler.RequestUris[1]);
        Assert.Equal("http://localhost:8080/instance/pair", handler.RequestUris[2]);
        Assert.Equal("instance-token", handler.ApiKeys[1]);
        Assert.Equal("instance-token", handler.ApiKeys[2]);
    }

    [Fact]
    public async Task GenerateQrCode_WhenAllQrEndpointsFailWithHttp_ReturnsError()
    {
        // Se todos os endpoints de QR retornarem erro HTTP (ex.: 404), deve retornar erro HTTP
        // ao invés de 202 Accepted — 202 é reservado para "QR ainda não disponível mas válido".
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var handler = new QueueHttpMessageHandler(
            // fetchInstances
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{"name":"test-instance","token":"instance-token","connectionStatus":"close"}]""")
            },
            // GET /qrcode com instance-token — 404
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("""{"message":"qrcode endpoint not found"}""")
            },
            // GET /qrcode com master-key — 404
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("""{"message":"qrcode endpoint not found"}""")
            },
            // GET /connect/{name} com instance-token — 404
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("""{"message":"connect endpoint not found"}""")
            },
            // GET /connect/{name} com master-key — 404
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("""{"message":"connect endpoint not found"}""")
            },
            // GET /instance/qr?instanceName={name} com instance-token — 404
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("""{"message":"qr endpoint not found"}""")
            },
            // GET /instance/qr?instanceName={name} com master-key — 404
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("""{"message":"qr endpoint not found"}""")
            },
            // GET /instance/qr com instance-token — 404
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("""{"message":"qr endpoint not found"}""")
            },
            // GET /instance/qr com master-key — 404
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("""{"message":"qr endpoint not found"}""")
            });

        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var controller = CreateController(context, farmacia.Id, factory.Object);
        var request = new QrCodeGenerateRequest("test-instance", "http://localhost:8080", "master-key");

        var result = await controller.GenerateQrCode(request);

        // Todos os endpoints falharam com HTTP error — não deve ser 202 Accepted
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.NotEqual(StatusCodes.Status202Accepted, objectResult.StatusCode);
        var json = System.Text.Json.JsonSerializer.Serialize(objectResult.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task GenerateQrCode_WhenInstanceTokenEqualsApiKey_UsesSingleApiKeyForQrEndpoints()
    {
        // Quando o token da instância é igual à chave mestre (ou não há token separado),
        // não deve duplicar as tentativas de QR com a mesma chave.
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var handler = new QueueHttpMessageHandler(
            // fetchInstances retorna token idêntico ao master-key
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{"name":"test-instance","token":"master-key","connectionStatus":"close"}]""")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"base64":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"}""")
            });

        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var controller = CreateController(context, farmacia.Id, factory.Object);
        var request = new QrCodeGenerateRequest("test-instance", "http://localhost:8080", "master-key");

        var result = await controller.GenerateQrCode(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        // Somente 2 requisições: fetchInstances + qrcode (sem duplicação por chave idêntica)
        Assert.Equal(2, handler.RequestUris.Count);
        Assert.Equal("http://localhost:8080/instance/test-instance/qrcode", handler.RequestUris[1]);
    }

    [Fact]
    public async Task GetWhatsAppInstanceStatus_UsesConnectionStateEndpoint()
    {
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var handler = new QueueHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{"name":"test-instance","token":"instance-token","connectionStatus":"close"}]""")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"instance":{"instanceName":"test-instance","state":"close"}}""")
            });

        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var controller = CreateController(context, farmacia.Id, factory.Object);

        var result = await controller.GetWhatsAppInstanceStatus("test-instance", "http://localhost:8080", "master-key");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal("close", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("http://localhost:8080/instance/fetchInstances?instanceName=test-instance", handler.RequestUris[0]);
        Assert.Equal("http://localhost:8080/instance/connectionState/test-instance", handler.RequestUris[1]);
    }

    [Fact]
    public async Task GetWhatsAppInstanceStatus_WhenConnectionStateFails_UsesFetchInstancesFallback()
    {
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var handler = new QueueHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{"name":"test-instance","token":"master-key","connectionStatus":"close"}]""")
            },
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("""{"error":"instance not found in connectionState"}""")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{"name":"test-instance","connectionStatus":"close"}]""")
            });

        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var controller = CreateController(context, farmacia.Id, factory.Object);

        var result = await controller.GetWhatsAppInstanceStatus("test-instance", "http://localhost:8080", "master-key");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal("close", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("http://localhost:8080/instance/fetchInstances?instanceName=test-instance", handler.RequestUris[0]);
        Assert.Equal("http://localhost:8080/instance/connectionState/test-instance", handler.RequestUris[1]);
        Assert.Equal("http://localhost:8080/instance/fetchInstances?instanceName=test-instance", handler.RequestUris[2]);
        Assert.DoesNotContain(handler.RequestUris, uri => uri.Contains("/instance/status", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ListWhatsAppInstances_UsesFetchInstancesEndpoint()
    {
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var handler = new QueueHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{"name":"instancia-a","connectionStatus":"open"},{"instance":{"instanceName":"instancia-b","connectionStatus":{"state":"close"}}}]""")
            });

        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var controller = CreateController(context, farmacia.Id, factory.Object);

        var result = await controller.ListWhatsAppInstances("http://localhost:8080", "master-key");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var instances = doc.RootElement.GetProperty("instances");

        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("http://localhost:8080/instance/fetchInstances", handler.RequestUris[0]);
        Assert.Equal(2, instances.GetArrayLength());
        Assert.Equal("instancia-a", instances[0].GetProperty("name").GetString());
        Assert.Equal("open", instances[0].GetProperty("status").GetString());
        Assert.Equal("instancia-b", instances[1].GetProperty("name").GetString());
        Assert.Equal("close", instances[1].GetProperty("status").GetString());
    }

    [Fact]
    public async Task ListWhatsAppInstances_UsesPersistedApiKeyWhenRequestOmitsIt()
    {
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        farmacia.Integracoes!.WhatsAppApiKey = "stored-key";
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var handler = new QueueHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{"name":"instancia-a","connectionStatus":"open"}]""")
            });

        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var controller = CreateController(context, farmacia.Id, factory.Object);

        var result = await controller.ListWhatsAppInstances("http://localhost:8080", "");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);

        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("stored-key", handler.ApiKeys[0]);
    }

    [Fact]
    public async Task ListWhatsAppInstances_WhenEvolutionRequestFails_ReturnsFriendlyConnectionError()
    {
        // Arrange
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var handler = new ThrowingHttpMessageHandler(new HttpRequestException("An error occurred while sending the request."));
        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var controller = CreateController(context, farmacia.Id, factory.Object);

        // Act
        var result = await controller.ListWhatsAppInstances("http://localhost:8080", "test-key");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        var doc = System.Text.Json.JsonDocument.Parse(json);
        var error = doc.RootElement.GetProperty("error").GetString() ?? string.Empty;

        Assert.Contains("Não foi possível conectar à Evolution Go API", error);
        Assert.DoesNotContain("An error occurred while sending the request.", error);
    }

    [Fact]
    public async Task GenerateQrCode_WhenLegacyRoutesReturnUnauthorized_ConnectsInstanceAndRetriesQr()
    {
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var connected = false;
        var handler = new CallbackHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/instance/fetchInstances", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""[{"name":"test-instance","token":"instance-token"}]""")
                };
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/instance/connect", StringComparison.Ordinal))
            {
                connected = true;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"message":"success"}""")
                };
            }

            if (connected && request.Method == HttpMethod.Get && path.EndsWith("/instance/qr", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"qrcode":{"base64":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"}}""")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("""{"error":"not authorized"}""")
            };
        });
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Application:ApiUrl"] = "https://api.example.com"
            })
            .Build();
        var controller = CreateController(context, farmacia.Id, factory.Object, configuration);

        var result = await controller.GenerateQrCode(
            new QrCodeGenerateRequest("test-instance", "http://localhost:8080", "master-key"));

        Assert.IsType<OkObjectResult>(result);
        Assert.Contains(handler.Requests, request =>
            request.Method == HttpMethod.Post &&
            request.Uri.EndsWith("/instance/connect", StringComparison.Ordinal));
        Assert.Contains(handler.Requests, request =>
            request.Method == HttpMethod.Get &&
            request.Uri.EndsWith("/instance/qr", StringComparison.Ordinal));
        Assert.Contains(
            handler.RequestBodies,
            body => body?.Contains("https://api.example.com/api/whatsapp/webhook", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task GenerateQrCode_WhenEvolutionReportsSessionAlreadyLoggedIn_ReturnsConnectedWithoutRetrying()
    {
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var handler = new QueueHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{"name":"test-instance","token":"instance-token"}]""")
            },
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("""{"error":"session already logged in"}""")
            });
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));
        var controller = CreateController(context, farmacia.Id, factory.Object);

        var result = await controller.GenerateQrCode(
            new QrCodeGenerateRequest("test-instance", "http://localhost:8080", "master-key"));

        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("connected").GetBoolean());
        Assert.Equal(2, handler.RequestUris.Count);
    }

    #region Regression — Evolution Webhook URL

    /// <summary>
    /// Regression test: BuildEvolutionCreatePayload must include 'webhook' as a plain URL string
    /// (not an object), plus 'webhookByEvents: true' and 'events' array.
    /// Previously the bug sent webhook as an object {url, enabled, events} which Evolution Go silently ignored.
    /// See: AGENTS.md > Fixed Issues > WhatsApp Evolution base URL
    /// </summary>
    [Fact]
    public async Task EnsureWhatsAppInstance_WhenCreatingInstance_IncludesWebhookAsStringInCreatePayload()
    {
        // Arrange
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var handler = new QueueHttpMessageHandler(
            // 1. fetchInstances — instance doesn't exist
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") },
            // 2. POST /instance/create — success
            new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("""{"instance":{"instanceName":"test-instance"}}""") },
            // 3. fetchInstances — resolve token for TryUpdateEvolutionWebhookAsync
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""[{"name":"test-instance","token":"instance-token","connectionStatus":"close"}]""") },
            // 4. POST /instance/connect — success
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"message":"success"}""") });

        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GLOBAL_API_KEY"] = "test-global-api-key",
                ["Application:ApiUrl"] = "https://api.example.com"
            })
            .Build();

        var controller = CreateController(context, farmacia.Id, factory.Object, configuration);
        var request = new QrCodeGenerateRequest(
            InstanceName: "test-instance",
            ApiUrl: "http://localhost:8080",
            ApiKey: "master-key");

        // Act
        var result = await controller.EnsureWhatsAppInstance(request);

        // Assert
        Assert.IsType<OkObjectResult>(result);

        // Verify create payload (2nd request = POST /instance/create)
        using var createDoc = System.Text.Json.JsonDocument.Parse(handler.RequestBodies[1]!);

        // webhook must be a plain URL string, NOT an object
        var webhookProp = createDoc.RootElement.GetProperty("webhook");
        Assert.Equal(System.Text.Json.JsonValueKind.String, webhookProp.ValueKind);
        Assert.Equal("https://api.example.com/api/whatsapp/webhook", webhookProp.GetString());

        // webhookByEvents must be true
        Assert.True(createDoc.RootElement.GetProperty("webhookByEvents").GetBoolean());

        // events must be a non-empty array
        var events = createDoc.RootElement.GetProperty("events");
        Assert.Equal(System.Text.Json.JsonValueKind.Array, events.ValueKind);
        Assert.NotEmpty(events.EnumerateArray());
    }

    /// <summary>
    /// Regression test: After creating a new instance, EnsureWhatsAppInstance must call
    /// POST /instance/connect with instance-specific token and correct webhookUrl.
    /// Previously this step was missing, so the webhook URL was never registered.
    /// See: AGENTS.md > Fixed Issues > WhatsApp Evolution base URL
    /// </summary>
    [Fact]
    public async Task EnsureWhatsAppInstance_WhenCreatingInstance_CallsConnectWithWebhookUrl()
    {
        // Arrange
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var handler = new QueueHttpMessageHandler(
            // 1. fetchInstances — instance doesn't exist
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") },
            // 2. POST /instance/create — success
            new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("""{"instance":{"instanceName":"test-instance"}}""") },
            // 3. fetchInstances — resolve token for connect
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""[{"name":"test-instance","token":"instance-token","connectionStatus":"close"}]""") },
            // 4. POST /instance/connect — success
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"message":"success"}""") });

        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GLOBAL_API_KEY"] = "test-global-api-key",
                ["Application:ApiUrl"] = "https://api.example.com"
            })
            .Build();

        var controller = CreateController(context, farmacia.Id, factory.Object, configuration);
        var request = new QrCodeGenerateRequest(
            InstanceName: "test-instance",
            ApiUrl: "http://localhost:8080",
            ApiKey: "master-key");

        // Act
        var result = await controller.EnsureWhatsAppInstance(request);

        // Assert
        Assert.IsType<OkObjectResult>(result);

        // Should have made 4 requests: fetchInstances, create, fetchInstances, connect
        Assert.Equal(4, handler.RequestUris.Count);
        Assert.Equal("http://localhost:8080/instance/fetchInstances?instanceName=test-instance", handler.RequestUris[0]);
        Assert.Equal("http://localhost:8080/instance/create", handler.RequestUris[1]);
        Assert.Equal("http://localhost:8080/instance/fetchInstances?instanceName=test-instance", handler.RequestUris[2]);
        Assert.Equal("http://localhost:8080/instance/connect", handler.RequestUris[3]);

        // Connect call must use instance-specific token, not master-key
        Assert.Equal("instance-token", handler.ApiKeys[3]);

        // Connect payload must contain webhookUrl
        using var connectDoc = System.Text.Json.JsonDocument.Parse(handler.RequestBodies[3]!);
        Assert.Equal("https://api.example.com/api/whatsapp/webhook", connectDoc.RootElement.GetProperty("webhookUrl").GetString());
    }

    /// <summary>
    /// Regression test: When the instance already exists, EnsureWhatsAppInstance must still
    /// call POST /instance/connect to update/ensure the webhook URL is configured.
    /// </summary>
    [Fact]
    public async Task EnsureWhatsAppInstance_WhenInstanceExists_CallsConnectWithWebhookUrl()
    {
        // Arrange
        var context = CreateDbContext();
        var farmacia = CreateTestFarmacia();
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var handler = new QueueHttpMessageHandler(
            // 1. fetchInstances — instance already exists
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""[{"name":"test-instance","token":"instance-token","connectionStatus":"close"}]""") },
            // 2. fetchInstances — resolve token for TryUpdateEvolutionWebhookAsync
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""[{"name":"test-instance","token":"instance-token","connectionStatus":"close"}]""") },
            // 3. POST /instance/connect — success
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"message":"success"}""") });

        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GLOBAL_API_KEY"] = "test-global-api-key",
                ["Application:ApiUrl"] = "https://api.example.com"
            })
            .Build();

        var controller = CreateController(context, farmacia.Id, factory.Object, configuration);
        var request = new QrCodeGenerateRequest(
            InstanceName: "test-instance",
            ApiUrl: "http://localhost:8080",
            ApiKey: "master-key");

        // Act
        var result = await controller.EnsureWhatsAppInstance(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("alreadyExists").GetBoolean());

        // Should have made 3 requests: fetchInstances, fetchInstances, connect
        Assert.Equal(3, handler.RequestUris.Count);
        Assert.Equal("http://localhost:8080/instance/connect", handler.RequestUris[2]);

        // Connect call must use instance-specific token
        Assert.Equal("instance-token", handler.ApiKeys[2]);

        // Connect payload must contain webhookUrl
        using var connectDoc = System.Text.Json.JsonDocument.Parse(handler.RequestBodies[2]!);
        Assert.Equal("https://api.example.com/api/whatsapp/webhook", connectDoc.RootElement.GetProperty("webhookUrl").GetString());
    }

    #endregion

    private sealed class ThrowingHttpMessageHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromException<HttpResponseMessage>(exception);
    }

    private sealed class QueueHttpMessageHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public List<string> RequestUris { get; } = new();
        public List<string?> ApiKeys { get; } = new();
        public List<string?> RequestBodies { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_responses.Count == 0)
                throw new InvalidOperationException("No mocked response available for the request.");

            RequestUris.Add(request.RequestUri!.ToString());
            ApiKeys.Add(request.Headers.TryGetValues("apikey", out var apiKeys) ? apiKeys.SingleOrDefault() : null);
            RequestBodies.Add(request.Content is null ? null : request.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult());
            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class CallbackHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> callback) : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Uri)> Requests { get; } = new();
        public List<string?> RequestBodies { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.Method, request.RequestUri!.ToString()));
            RequestBodies.Add(request.Content is null
                ? null
                : request.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult());
            return Task.FromResult(callback(request));
        }
    }
}
