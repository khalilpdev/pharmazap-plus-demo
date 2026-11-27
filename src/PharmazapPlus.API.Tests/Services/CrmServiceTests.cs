using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using PharmazapPlus.API.Data;
using PharmazapPlus.API.Hubs;
using PharmazapPlus.API.Services.CRM;
using PharmazapPlus.API.Services.WhatsApp;
using PharmazapPlus.Shared.Enums;
using PharmazapPlus.Shared.Models.CRM;
using FluentAssertions;

namespace PharmazapPlus.API.Tests.Services;

public class CrmServiceTests
{
    private readonly Mock<ILogger<CrmService>> _mockLogger;
    private readonly Mock<IWhatsAppService> _mockWhatsAppService;
    private readonly Mock<IHubContext<PharmaHub>> _mockHubContext;
    private readonly Mock<IClienteImporter> _mockClienteImporter;
    private readonly Mock<IClienteExporter> _mockClienteExporter;

    public CrmServiceTests()
    {
        _mockLogger = new Mock<ILogger<CrmService>>();
        _mockWhatsAppService = new Mock<IWhatsAppService>();
        _mockHubContext = new Mock<IHubContext<PharmaHub>>();
        _mockClienteImporter = new Mock<IClienteImporter>();
        _mockClienteExporter = new Mock<IClienteExporter>();
    }

    [Fact]
    public async Task GetOrCreateClienteByWhatsApp_WithInvalidFarmaciaId_ThrowsException()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PharmazapDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new PharmazapDbContext(options);
        
        var service = new CrmService(
            context,
            _mockLogger.Object,
            _mockWhatsAppService.Object,
            _mockHubContext.Object,
            _mockClienteImporter.Object,
            _mockClienteExporter.Object);
        
        var invalidFarmaciaId = "invalid";
        var whatsAppId = "55562998808389";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetOrCreateClienteByWhatsApp(invalidFarmaciaId, whatsAppId));
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PharmazapDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new PharmazapDbContext(options);

        // Act
        var service = new CrmService(
            context,
            _mockLogger.Object,
            _mockWhatsAppService.Object,
            _mockHubContext.Object,
            _mockClienteImporter.Object,
            _mockClienteExporter.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void CrmService_CanBeInstantiatedWithDependencies()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PharmazapDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new PharmazapDbContext(options);

        // Act
        var service = new CrmService(
            context,
            _mockLogger.Object,
            _mockWhatsAppService.Object,
            _mockHubContext.Object,
            _mockClienteImporter.Object,
            _mockClienteExporter.Object);

        // Assert
        service.Should().NotBeNull();
        service.Should().BeAssignableTo<ICrmService>();
    }

    [Fact]
    public async Task GetOrCreateClienteByWhatsApp_WithCountryCodeAndMask_ReusesExistingCliente()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PharmazapDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        await using var context = new PharmazapDbContext(options);

        var existente = new Cliente
        {
            FarmaciaId = 1,
            Farmacia = null!,
            WhatsAppId = "(62) 99880-8389",
            Nome = "Cliente Existente",
            CodigoCliente = "CLI-0001"
        };
        context.Clientes.Add(existente);
        await context.SaveChangesAsync();

        var service = new CrmService(
            context,
            _mockLogger.Object,
            _mockWhatsAppService.Object,
            _mockHubContext.Object,
            _mockClienteImporter.Object,
            _mockClienteExporter.Object);

        // Act
        var cliente = await service.GetOrCreateClienteByWhatsApp("1", "5562998808389");

        // Assert
        cliente.Id.Should().Be(existente.Id);
        cliente.WhatsAppId.Should().Be("6298808389");
        context.Clientes.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetOrCreateClienteByWhatsApp_WithoutDddAndWithoutNinthDigit_ReusesExistingCliente()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PharmazapDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        await using var context = new PharmazapDbContext(options);

        var existente = new Cliente
        {
            FarmaciaId = 1,
            Farmacia = null!,
            WhatsAppId = "6298808389",
            Nome = "Cliente Existente",
            CodigoCliente = "CLI-0001"
        };
        context.Clientes.Add(existente);
        await context.SaveChangesAsync();

        var service = new CrmService(
            context,
            _mockLogger.Object,
            _mockWhatsAppService.Object,
            _mockHubContext.Object,
            _mockClienteImporter.Object,
            _mockClienteExporter.Object);

        // Act
        var cliente = await service.GetOrCreateClienteByWhatsApp("1", "98808389");

        // Assert
        cliente.Id.Should().Be(existente.Id);
        cliente.WhatsAppId.Should().Be("6298808389");
        context.Clientes.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetOrCreateClienteByWhatsApp_WithDifferentPrefixAndSameLastEightDigits_ReusesExistingCliente()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PharmazapDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        await using var context = new PharmazapDbContext(options);

        var existente = new Cliente
        {
            FarmaciaId = 1,
            Farmacia = null!,
            WhatsAppId = "1112345678",
            Nome = "Cliente Existente",
            CodigoCliente = "CLI-0001"
        };
        context.Clientes.Add(existente);
        await context.SaveChangesAsync();

        var service = new CrmService(
            context,
            _mockLogger.Object,
            _mockWhatsAppService.Object,
            _mockHubContext.Object,
            _mockClienteImporter.Object,
            _mockClienteExporter.Object);

        // Act
        var cliente = await service.GetOrCreateClienteByWhatsApp("1", "5531912345678");

        // Assert
        cliente.Id.Should().Be(existente.Id);
        context.Clientes.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetOrCreateClienteByWhatsApp_WithDifferentLastEightDigits_CreatesNewCliente()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PharmazapDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        await using var context = new PharmazapDbContext(options);

        var existente = new Cliente
        {
            FarmaciaId = 1,
            Farmacia = null!,
            WhatsAppId = "1112345678",
            Nome = "Cliente Existente",
            CodigoCliente = "CLI-0001"
        };
        context.Clientes.Add(existente);
        await context.SaveChangesAsync();

        var service = new CrmService(
            context,
            _mockLogger.Object,
            _mockWhatsAppService.Object,
            _mockHubContext.Object,
            _mockClienteImporter.Object,
            _mockClienteExporter.Object);

        // Act
        var cliente = await service.GetOrCreateClienteByWhatsApp("1", "5531919995678");

        // Assert
        cliente.Id.Should().NotBe(existente.Id);
        context.Clientes.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetOrCreateClienteByCanalContato_Instagram_DoesNotDuplicateCliente()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PharmazapDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        await using var context = new PharmazapDbContext(options);

        var service = new CrmService(
            context,
            _mockLogger.Object,
            _mockWhatsAppService.Object,
            _mockHubContext.Object,
            _mockClienteImporter.Object,
            _mockClienteExporter.Object);

        // Act
        var first = await service.GetOrCreateClienteByCanalContato("1", "17841400000000001", CanalEnum.Instagram);
        var second = await service.GetOrCreateClienteByCanalContato("1", "17841400000000001", CanalEnum.Instagram);

        // Assert
        second.Id.Should().Be(first.Id);
        context.Clientes.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetOrCreateClienteByWhatsApp_WithContactName_PersistsNomeAndTelefone()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PharmazapDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        await using var context = new PharmazapDbContext(options);

        var service = new CrmService(
            context,
            _mockLogger.Object,
            _mockWhatsAppService.Object,
            _mockHubContext.Object,
            _mockClienteImporter.Object,
            _mockClienteExporter.Object);

        // Act
        var cliente = await service.GetOrCreateClienteByWhatsApp("1", "5511999999999", "Maria Silva");

        // Assert
        cliente.Nome.Should().Be("Maria Silva");
        cliente.Telefone.Should().Be("1199999999");
        cliente.WhatsAppId.Should().Be("1199999999");
    }
}
