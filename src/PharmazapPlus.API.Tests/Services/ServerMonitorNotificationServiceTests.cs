using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using PharmazapPlus.API.Data;
using PharmazapPlus.API.Models;
using PharmazapPlus.API.Services.Monitoring;
using PharmazapPlus.API.Services.WhatsApp;
using PharmazapPlus.Shared.Models.Core;

namespace PharmazapPlus.API.Tests.Services;

public class ServerMonitorNotificationServiceTests
{
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<IWhatsAppService> _whatsAppServiceMock;
    private readonly Mock<ILogger<ServerMonitorNotificationService>> _loggerMock;

    public ServerMonitorNotificationServiceTests()
    {
        _emailServiceMock = new Mock<IEmailService>();
        _whatsAppServiceMock = new Mock<IWhatsAppService>();
        _loggerMock = new Mock<ILogger<ServerMonitorNotificationService>>();
    }

    private PharmazapDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PharmazapDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PharmazapDbContext(options);
    }

    private static Farmacia CreateFarmacia(string cnpj) => new()
    {
        Nome = "Farmácia Teste",
        Cnpj = cnpj,
        Telefone = "(62) 3333-3333",
        Email = "farmacia@teste.com",
        Endereco = "Rua Teste, 1",
        Cidade = "Goiânia",
        Estado = "GO",
        Cep = "74000000",
        Integracoes = new FarmaciaIntegracoes
        {
            ChaveWhatsApp = $"key-{cnpj}",
            WhatsAppPhoneNumberId = "",
            WhatsAppAccessToken = "",
            WhatsAppBusinessAccountId = ""
        }
    };

    private static UsuarioFarmacia CreateUsuario(int farmaciaId, string nome, string email, 
        string role, bool ativo = true, string? telefone = null) => new()
    {
        FarmaciaId = farmaciaId,
        Farmacia = null!,
        Nome = nome,
        Email = email,
        Telefone = telefone ?? "",
        Role = role,
        Ativo = ativo,
        EmailConfirmado = true,
        PasswordHash = "hash",
        PasswordSalt = "salt",
        DataCadastro = DateTime.UtcNow,
        Turno = "Integral",
        RecebeNotificacoes = true,
        TotalAtendimentos = 0,
        AtendimentosHoje = 0
    };

    private ServerMonitorNotificationService CreateService(
        HealthMonitorConfig? config = null,
        PharmazapDbContext? dbContext = null)
    {
        var cfg = config ?? new HealthMonitorConfig
        {
            NotifyViaWhatsApp = true,
            NotifyViaEmail = true,
            FetchAdminsFromDatabase = false,
            AdminWhatsAppNumbers = [],
            AdminEmails = [],
            ServerName = "TestServer"
        };

        var services = new ServiceCollection();
        services.AddScoped(_ => _whatsAppServiceMock.Object);
        if (dbContext != null)
            services.AddScoped(_ => dbContext);
        else
            services.AddScoped(_ => CreateDbContext());

        var serviceProvider = services.BuildServiceProvider();

        return new ServerMonitorNotificationService(
            serviceProvider,
            _emailServiceMock.Object,
            Options.Create(cfg),
            _loggerMock.Object);
    }

    // ─────────────────────────────────────────────────────────
    // NotifyServerDown
    // ─────────────────────────────────────────────────────────

    [Fact]
    public async Task NotifyServerDown_WithWhatsAppNumbers_SendsWhatsAppMessage()
    {
        // Arrange
        var config = new HealthMonitorConfig
        {
            NotifyViaWhatsApp = true,
            NotifyViaEmail = false,
            FetchAdminsFromDatabase = false,
            AdminWhatsAppNumbers = ["5562999999999"],
            AdminEmails = [],
            ServerName = "TestServer"
        };

        _whatsAppServiceMock
            .Setup(x => x.SendTextMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new WhatsAppMessageResponse { Success = true, MessageId = "msg-1" });

        var service = CreateService(config);

        // Act
        await service.NotifyServerDown("TestServer", ["database"], DateTime.UtcNow);

        // Assert
        _whatsAppServiceMock.Verify(
            x => x.SendTextMessage("0", "5562999999999", It.Is<string>(m => m.Contains("Fora do Ar") && m.Contains("database"))),
            Times.Once);
    }

    [Fact]
    public async Task NotifyServerDown_WithEmails_SendsEmailMessage()
    {
        // Arrange
        var config = new HealthMonitorConfig
        {
            NotifyViaWhatsApp = false,
            NotifyViaEmail = true,
            FetchAdminsFromDatabase = false,
            AdminWhatsAppNumbers = [],
            AdminEmails = ["admin@farmacia.com"],
            ServerName = "TestServer"
        };

        _emailServiceMock
            .Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var service = CreateService(config);

        // Act
        await service.NotifyServerDown("TestServer", ["redis"], DateTime.UtcNow);

        // Assert
        _emailServiceMock.Verify(
            x => x.SendEmailAsync(
                "admin@farmacia.com",
                It.Is<string>(s => s.Contains("Fora do Ar")),
                It.Is<string>(b => b.Contains("redis"))),
            Times.Once);
    }

    [Fact]
    public async Task NotifyServerDown_WithWhatsAppDisabled_DoesNotSendWhatsApp()
    {
        // Arrange
        var config = new HealthMonitorConfig
        {
            NotifyViaWhatsApp = false,
            NotifyViaEmail = true,
            FetchAdminsFromDatabase = false,
            AdminWhatsAppNumbers = ["5562999999999"],
            AdminEmails = ["admin@farmacia.com"],
            ServerName = "TestServer"
        };

        _emailServiceMock
            .Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var service = CreateService(config);

        // Act
        await service.NotifyServerDown("TestServer", ["database"], DateTime.UtcNow);

        // Assert
        _whatsAppServiceMock.Verify(
            x => x.SendTextMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task NotifyServerDown_WithEmailDisabled_DoesNotSendEmail()
    {
        // Arrange
        var config = new HealthMonitorConfig
        {
            NotifyViaWhatsApp = true,
            NotifyViaEmail = false,
            FetchAdminsFromDatabase = false,
            AdminWhatsAppNumbers = ["5562999999999"],
            AdminEmails = ["admin@farmacia.com"],
            ServerName = "TestServer"
        };

        _whatsAppServiceMock
            .Setup(x => x.SendTextMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new WhatsAppMessageResponse { Success = true });

        var service = CreateService(config);

        // Act
        await service.NotifyServerDown("TestServer", ["database"], DateTime.UtcNow);

        // Assert
        _emailServiceMock.Verify(
            x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task NotifyServerDown_WithNoContacts_DoesNotThrow()
    {
        // Arrange
        var config = new HealthMonitorConfig
        {
            NotifyViaWhatsApp = true,
            NotifyViaEmail = true,
            FetchAdminsFromDatabase = false,
            AdminWhatsAppNumbers = [],
            AdminEmails = [],
            ServerName = "TestServer"
        };

        var service = CreateService(config);

        // Act & Assert
        var act = async () => await service.NotifyServerDown("TestServer", ["database"], DateTime.UtcNow);
        await act.Should().NotThrowAsync();

        _whatsAppServiceMock.Verify(
            x => x.SendTextMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        _emailServiceMock.Verify(
            x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    // ─────────────────────────────────────────────────────────
    // NotifyServerRecovered
    // ─────────────────────────────────────────────────────────

    [Fact]
    public async Task NotifyServerRecovered_WithWhatsAppNumbers_SendsRecoveryMessage()
    {
        // Arrange
        var config = new HealthMonitorConfig
        {
            NotifyViaWhatsApp = true,
            NotifyViaEmail = false,
            FetchAdminsFromDatabase = false,
            AdminWhatsAppNumbers = ["5562999999999"],
            AdminEmails = [],
            ServerName = "TestServer"
        };

        _whatsAppServiceMock
            .Setup(x => x.SendTextMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new WhatsAppMessageResponse { Success = true });

        var service = CreateService(config);

        // Act
        await service.NotifyServerRecovered("TestServer", TimeSpan.FromMinutes(5));

        // Assert
        _whatsAppServiceMock.Verify(
            x => x.SendTextMessage("0", "5562999999999", It.Is<string>(m => m.Contains("RECUPERADO") || m.Contains("Online"))),
            Times.Once);
    }

    [Fact]
    public async Task NotifyServerRecovered_WithEmails_SendsRecoveryEmail()
    {
        // Arrange
        var config = new HealthMonitorConfig
        {
            NotifyViaWhatsApp = false,
            NotifyViaEmail = true,
            FetchAdminsFromDatabase = false,
            AdminWhatsAppNumbers = [],
            AdminEmails = ["admin@farmacia.com"],
            ServerName = "TestServer"
        };

        _emailServiceMock
            .Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var service = CreateService(config);

        // Act
        await service.NotifyServerRecovered("TestServer", TimeSpan.FromMinutes(10));

        // Assert
        _emailServiceMock.Verify(
            x => x.SendEmailAsync(
                "admin@farmacia.com",
                It.Is<string>(s => s.Contains("RECUPERADO")),
                It.IsAny<string>()),
            Times.Once);
    }

    // ─────────────────────────────────────────────────────────
    // GetAdminContactsAsync — database
    // ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAdminContactsAsync_FetchesGestorAndAdministradorFromDatabase()
    {
        // Arrange
        var dbContext = CreateDbContext();
        var farmacia = CreateFarmacia("00000000000000");
        dbContext.Farmacias.Add(farmacia);
        await dbContext.SaveChangesAsync();

        dbContext.UsuariosFarmacia.AddRange(
            CreateUsuario(farmacia.Id, "Gestor 1", "gestor@test.com", UserRoles.Gestor, telefone: "62999990001"),
            CreateUsuario(farmacia.Id, "Admin 1", "admin@test.com", UserRoles.Administrador, telefone: "62999990002"),
            CreateUsuario(farmacia.Id, "Atendente 1", "atendente@test.com", UserRoles.Atendente, telefone: "62999990003")
        );
        await dbContext.SaveChangesAsync();

        var config = new HealthMonitorConfig
        {
            FetchAdminsFromDatabase = true,
            AdminWhatsAppNumbers = [],
            AdminEmails = [],
            NotifyViaWhatsApp = true,
            NotifyViaEmail = true,
            ServerName = "TestServer"
        };

        var service = CreateService(config, dbContext);

        // Act
        var (whatsAppNumbers, emails) = await service.GetAdminContactsAsync();

        // Assert — should contain gestor and admin, NOT atendente
        emails.Should().Contain("gestor@test.com");
        emails.Should().Contain("admin@test.com");
        emails.Should().NotContain("atendente@test.com");
        whatsAppNumbers.Should().Contain("5562999990001");
        whatsAppNumbers.Should().Contain("5562999990002");
        whatsAppNumbers.Should().NotContain("5562999990003");
    }

    [Fact]
    public async Task GetAdminContactsAsync_ExcludesInactiveAdmins()
    {
        // Arrange
        var dbContext = CreateDbContext();
        var farmacia = CreateFarmacia("11111111111111");
        dbContext.Farmacias.Add(farmacia);
        await dbContext.SaveChangesAsync();

        dbContext.UsuariosFarmacia.AddRange(
            CreateUsuario(farmacia.Id, "Gestor Ativo", "gestor.ativo@test.com", UserRoles.Gestor, telefone: "62999991001"),
            CreateUsuario(farmacia.Id, "Gestor Inativo", "gestor.inativo@test.com", UserRoles.Gestor, ativo: false, telefone: "62999991002")
        );
        await dbContext.SaveChangesAsync();

        var config = new HealthMonitorConfig
        {
            FetchAdminsFromDatabase = true,
            AdminWhatsAppNumbers = [],
            AdminEmails = [],
            NotifyViaWhatsApp = true,
            NotifyViaEmail = true,
            ServerName = "TestServer"
        };

        var service = CreateService(config, dbContext);

        // Act
        var (_, emails) = await service.GetAdminContactsAsync();

        // Assert
        emails.Should().Contain("gestor.ativo@test.com");
        emails.Should().NotContain("gestor.inativo@test.com");
    }

    [Fact]
    public async Task GetAdminContactsAsync_CombinesStaticConfigAndDatabaseContacts()
    {
        // Arrange
        var dbContext = CreateDbContext();
        var farmacia = CreateFarmacia("22222222222222");
        dbContext.Farmacias.Add(farmacia);
        await dbContext.SaveChangesAsync();

        dbContext.UsuariosFarmacia.Add(
            CreateUsuario(farmacia.Id, "DB Admin", "db@test.com", UserRoles.Administrador, telefone: "62999992001")
        );
        await dbContext.SaveChangesAsync();

        var config = new HealthMonitorConfig
        {
            FetchAdminsFromDatabase = true,
            AdminWhatsAppNumbers = ["5511999990000"],
            AdminEmails = ["static@config.com"],
            NotifyViaWhatsApp = true,
            NotifyViaEmail = true,
            ServerName = "TestServer"
        };

        var service = CreateService(config, dbContext);

        // Act
        var (whatsAppNumbers, emails) = await service.GetAdminContactsAsync();

        // Assert — both static config and DB contacts present
        emails.Should().Contain("static@config.com");
        emails.Should().Contain("db@test.com");
        whatsAppNumbers.Should().Contain("5511999990000");
        whatsAppNumbers.Should().Contain("5562999992001");
    }

    [Fact]
    public async Task GetAdminContactsAsync_WhenDatabaseFetchDisabled_UsesOnlyStaticConfig()
    {
        // Arrange
        var dbContext = CreateDbContext();
        var farmacia = CreateFarmacia("33333333333333");
        dbContext.Farmacias.Add(farmacia);
        await dbContext.SaveChangesAsync();

        dbContext.UsuariosFarmacia.Add(
            CreateUsuario(farmacia.Id, "DB Admin", "db@test.com", UserRoles.Administrador)
        );
        await dbContext.SaveChangesAsync();

        var config = new HealthMonitorConfig
        {
            FetchAdminsFromDatabase = false,
            AdminWhatsAppNumbers = [],
            AdminEmails = ["static@config.com"],
            NotifyViaWhatsApp = true,
            NotifyViaEmail = true,
            ServerName = "TestServer"
        };

        var service = CreateService(config, dbContext);

        // Act
        var (_, emails) = await service.GetAdminContactsAsync();

        // Assert — only static config, NOT db contacts
        emails.Should().Contain("static@config.com");
        emails.Should().NotContain("db@test.com");
    }

    // ─────────────────────────────────────────────────────────
    // CleanPhoneNumber
    // ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("(62) 99999-0001", "5562999990001")]
    [InlineData("62999990001", "5562999990001")]     // 11 digits — add BR country code
    [InlineData("6299990001", "556299990001")]        // 10 digits — add BR country code
    [InlineData("5562999990001", "5562999990001")]   // already has country code
    [InlineData("+55 62 99999-0001", "5562999990001")]
    public void CleanPhoneNumber_NormalizesPhoneNumberCorrectly(string input, string expected)
    {
        // Act
        var result = ServerMonitorNotificationService.CleanPhoneNumber(input);

        // Assert
        result.Should().Be(expected);
    }

    // ─────────────────────────────────────────────────────────
    // FormatDowntime
    // ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 45, "45 segundo(s)")]
    [InlineData(5, 30, "5 minuto(s) e 30 segundo(s)")]
    [InlineData(90, 0, "1h 30m 0s")]
    public void FormatDowntime_FormatsCorrectly(int minutes, int seconds, string expected)
    {
        // Act
        var result = ServerMonitorNotificationService.FormatDowntime(
            TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds));

        // Assert
        result.Should().Be(expected);
    }

    // ─────────────────────────────────────────────────────────
    // WhatsApp send failure tolerance
    // ─────────────────────────────────────────────────────────

    [Fact]
    public async Task NotifyServerDown_WhenWhatsAppFails_ContinuesToSendEmail()
    {
        // Arrange
        var config = new HealthMonitorConfig
        {
            NotifyViaWhatsApp = true,
            NotifyViaEmail = true,
            FetchAdminsFromDatabase = false,
            AdminWhatsAppNumbers = ["5562999999999"],
            AdminEmails = ["admin@farmacia.com"],
            ServerName = "TestServer"
        };

        _whatsAppServiceMock
            .Setup(x => x.SendTextMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new WhatsAppMessageResponse { Success = false, ErrorMessage = "API error" });

        _emailServiceMock
            .Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var service = CreateService(config);

        // Act
        var act = async () => await service.NotifyServerDown("TestServer", ["database"], DateTime.UtcNow);

        // Assert — no exception; email still sent despite WhatsApp failure
        await act.Should().NotThrowAsync();
        _emailServiceMock.Verify(
            x => x.SendEmailAsync("admin@farmacia.com", It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task NotifyServerDown_WhenWhatsAppThrows_ContinuesToSendEmail()
    {
        // Arrange
        var config = new HealthMonitorConfig
        {
            NotifyViaWhatsApp = true,
            NotifyViaEmail = true,
            FetchAdminsFromDatabase = false,
            AdminWhatsAppNumbers = ["5562999999999"],
            AdminEmails = ["admin@farmacia.com"],
            ServerName = "TestServer"
        };

        _whatsAppServiceMock
            .Setup(x => x.SendTextMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Connection refused"));

        _emailServiceMock
            .Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var service = CreateService(config);

        // Act
        var act = async () => await service.NotifyServerDown("TestServer", ["database"], DateTime.UtcNow);

        // Assert — no exception propagated; email still attempted
        await act.Should().NotThrowAsync();
        _emailServiceMock.Verify(
            x => x.SendEmailAsync("admin@farmacia.com", It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task NotifyServerDown_WithMultipleRecipients_SendsToAll()
    {
        // Arrange
        var config = new HealthMonitorConfig
        {
            NotifyViaWhatsApp = true,
            NotifyViaEmail = true,
            FetchAdminsFromDatabase = false,
            AdminWhatsAppNumbers = ["5562999991111", "5562999992222"],
            AdminEmails = ["admin1@farmacia.com", "admin2@farmacia.com"],
            ServerName = "TestServer"
        };

        _whatsAppServiceMock
            .Setup(x => x.SendTextMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new WhatsAppMessageResponse { Success = true });

        _emailServiceMock
            .Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var service = CreateService(config);

        // Act
        await service.NotifyServerDown("TestServer", ["database"], DateTime.UtcNow);

        // Assert
        _whatsAppServiceMock.Verify(
            x => x.SendTextMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Exactly(2));
        _emailServiceMock.Verify(
            x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Exactly(2));
    }
}
