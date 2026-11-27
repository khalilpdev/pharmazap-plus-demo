using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using PharmazapPlus.API.Controllers;
using PharmazapPlus.API.Services.CRM;
using PharmazapPlus.Shared.Enums;
using PharmazapPlus.Shared.Models.CRM;
using System.Security.Claims;
using FluentAssertions;

namespace PharmazapPlus.API.Tests.Controllers;

public class ClientesControllerTests
{
    private readonly Mock<ICrmService> _mockCrmService;
    private readonly Mock<ILogger<ClientesController>> _mockLogger;
    private readonly ClientesController _controller;

    public ClientesControllerTests()
    {
        _mockCrmService = new Mock<ICrmService>();
        _mockLogger = new Mock<ILogger<ClientesController>>();
        _controller = new ClientesController(_mockCrmService.Object, _mockLogger.Object);
        
        // Setup user claims
        var claims = new List<Claim>
        {
            new Claim("FarmaciaId", "1"),
            new Claim("FarmaciaNome", "Farmacia Teste")
        };
        
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);
        
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    private ClientesController CreateControllerWithoutFarmacia()
    {
        var controller = new ClientesController(_mockCrmService.Object, _mockLogger.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return controller;
    }

    // ── GET /api/clientes ────────────────────────────────────────────

    [Fact]
    public async Task GetClientes_WithValidFarmacia_ReturnsClientList()
    {
        // Arrange
        var mockClientes = new List<Cliente>
        {
            new Cliente
            {
                Id = 1,
                Nome = "Cliente Teste",
                WhatsAppId = "11999999999",
                Farmacia = null!,
                Telefone = "11999999999",
                Status = ClienteStatus.Ativo
            }
        };

        _mockCrmService
            .Setup(s => s.SearchClientes(It.IsAny<string>(), It.IsAny<ClienteSearchFilter>()))
            .ReturnsAsync(mockClientes);

        // Act
        var result = await _controller.GetClientes();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task GetClientes_WithoutFarmaciaId_ReturnsBadRequest()
    {
        // Arrange
        var controller = CreateControllerWithoutFarmacia();

        // Act
        var result = await controller.GetClientes();

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetClientes_WithSearchQuery_CallsServiceWithCorrectFilter()
    {
        // Arrange
        var searchQuery = "test";
        var mockClientes = new List<Cliente>();
        
        _mockCrmService
            .Setup(s => s.SearchClientes(It.IsAny<string>(), It.Is<ClienteSearchFilter>(f => f.Query == searchQuery)))
            .ReturnsAsync(mockClientes);

        // Act
        var result = await _controller.GetClientes(query: searchQuery);

        // Assert
        _mockCrmService.Verify(
            s => s.SearchClientes(It.IsAny<string>(), It.Is<ClienteSearchFilter>(f => f.Query == searchQuery)),
            Times.Once);
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        var controller = new ClientesController(_mockCrmService.Object, _mockLogger.Object);

        // Assert
        controller.Should().NotBeNull();
    }

    // ── GET /api/clientes/{id} ───────────────────────────────────────

    [Fact]
    public async Task GetCliente_WithValidId_ReturnsCliente()
    {
        // Arrange
        var cliente = new Cliente { Id = 1, Nome = "Cliente Teste", WhatsAppId = "5511999999999", FarmaciaId = 1, Farmacia = null! };
        _mockCrmService.Setup(s => s.GetCliente(1, "1")).ReturnsAsync(cliente);

        // Act
        var result = await _controller.GetCliente(1);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task GetCliente_WithoutFarmaciaId_ReturnsBadRequest()
    {
        // Arrange
        var controller = CreateControllerWithoutFarmacia();

        // Act
        var result = await controller.GetCliente(1);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetCliente_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        _mockCrmService.Setup(s => s.GetCliente(9999, "1")).ThrowsAsync(new NotFoundException("Cliente não encontrado"));

        // Act
        var result = await _controller.GetCliente(9999);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
        var notFound = result as NotFoundObjectResult;
        notFound!.StatusCode.Should().Be(404);
    }

    // ── POST /api/clientes ───────────────────────────────────────────

    [Fact]
    public async Task CreateCliente_WithValidData_ReturnsCreatedResult()
    {
        // Arrange
        var cliente = new Cliente { Nome = "Novo Cliente", WhatsAppId = "5511999999999", Farmacia = null! };
        var createdCliente = new Cliente { Id = 1, Nome = "Novo Cliente", WhatsAppId = "5511999999999", FarmaciaId = 1, Farmacia = null! };

        _mockCrmService
            .Setup(s => s.SearchClientes("1", It.IsAny<ClienteSearchFilter>()))
            .ReturnsAsync(new List<Cliente>());
        _mockCrmService
            .Setup(s => s.CreateCliente(It.IsAny<Cliente>()))
            .ReturnsAsync(createdCliente);

        // Act
        var result = await _controller.CreateCliente(cliente);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
        var created = result as CreatedAtActionResult;
        created!.StatusCode.Should().Be(201);
    }

    [Fact]
    public async Task CreateCliente_WithoutFarmaciaId_ReturnsBadRequest()
    {
        // Arrange
        var controller = CreateControllerWithoutFarmacia();
        var cliente = new Cliente { Nome = "Novo Cliente", WhatsAppId = "5511999999999", Farmacia = null! };

        // Act
        var result = await controller.CreateCliente(cliente);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateCliente_WithoutNome_ReturnsBadRequest()
    {
        // Arrange
        var cliente = new Cliente { Nome = "", WhatsAppId = "5511999999999", Farmacia = null! };

        // Act
        var result = await _controller.CreateCliente(cliente);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest!.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task CreateCliente_WithoutWhatsAppId_ReturnsBadRequest()
    {
        // Arrange
        var cliente = new Cliente { Nome = "Novo Cliente", WhatsAppId = "", Farmacia = null! };

        // Act
        var result = await _controller.CreateCliente(cliente);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest!.StatusCode.Should().Be(400);
    }

    // ── PUT /api/clientes/{id} ───────────────────────────────────────

    [Fact]
    public async Task UpdateCliente_WithValidData_ReturnsOkResult()
    {
        // Arrange
        var existingCliente = new Cliente { Id = 1, Nome = "Cliente", WhatsAppId = "5511999999999", FarmaciaId = 1, Farmacia = null! };
        var updatedCliente = new Cliente { Id = 1, Nome = "Nome Atualizado", WhatsAppId = "5511999999999", FarmaciaId = 1, Farmacia = null! };
        var updateDto = new ClienteUpdateDto { Nome = "Nome Atualizado" };

        _mockCrmService.Setup(s => s.GetCliente(1, "1")).ReturnsAsync(existingCliente);
        _mockCrmService.Setup(s => s.UpdateCliente(1, It.IsAny<ClienteUpdateDto>())).ReturnsAsync(updatedCliente);

        // Act
        var result = await _controller.UpdateCliente(1, updateDto);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task UpdateCliente_WithoutFarmaciaId_ReturnsBadRequest()
    {
        // Arrange
        var controller = CreateControllerWithoutFarmacia();
        var updateDto = new ClienteUpdateDto { Nome = "Nome Atualizado" };

        // Act
        var result = await controller.UpdateCliente(1, updateDto);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateCliente_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        _mockCrmService.Setup(s => s.GetCliente(9999, "1")).ThrowsAsync(new NotFoundException("Cliente não encontrado"));
        var updateDto = new ClienteUpdateDto { Nome = "Nome Atualizado" };

        // Act
        var result = await _controller.UpdateCliente(9999, updateDto);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
        var notFound = result as NotFoundObjectResult;
        notFound!.StatusCode.Should().Be(404);
    }

    // ── DELETE /api/clientes/{id} ────────────────────────────────────

    [Fact]
    public async Task DeleteCliente_WithValidId_ReturnsOkResult()
    {
        // Arrange
        _mockCrmService.Setup(s => s.DeleteCliente(1, "1")).ReturnsAsync((true, string.Empty));

        // Act
        var result = await _controller.DeleteCliente(1);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task DeleteCliente_WithoutFarmaciaId_ReturnsBadRequest()
    {
        // Arrange
        var controller = CreateControllerWithoutFarmacia();

        // Act
        var result = await controller.DeleteCliente(1);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── Prospect / nullable fields tests ────────────────────────────

    [Fact]
    public async Task CreateCliente_WithoutSobrenome_ReturnsCreated()
    {
        // Prospect sem sobrenome deve ser aceito (Sobrenome é opcional)
        var cliente = new Cliente { Nome = "Fulano", WhatsAppId = "5511999990001", Farmacia = null! };
        var createdCliente = new Cliente { Id = 2, Nome = "Fulano", WhatsAppId = "5511999990001", FarmaciaId = 1, Farmacia = null! };

        _mockCrmService
            .Setup(s => s.SearchClientes("1", It.IsAny<ClienteSearchFilter>()))
            .ReturnsAsync(new List<Cliente>());
        _mockCrmService
            .Setup(s => s.CreateCliente(It.IsAny<Cliente>()))
            .ReturnsAsync(createdCliente);

        // Act
        var result = await _controller.CreateCliente(cliente);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
        var created = result as CreatedAtActionResult;
        created!.StatusCode.Should().Be(201);
    }

    [Fact]
    public async Task CreateCliente_WithOnlyWhatsAppId_CreatesProspecto()
    {
        // Criação automática via WhatsApp com dados mínimos — PerfilCompra deve ser Prospecto
        var cliente = new Cliente
        {
            Nome = "Cliente WhatsApp 5511999990002",
            WhatsAppId = "5511999990002",
            Farmacia = null!,
            PerfilCompra = ClientePerfilCompraEnum.Prospecto
        };
        var createdCliente = new Cliente
        {
            Id = 3,
            Nome = "Cliente WhatsApp 5511999990002",
            WhatsAppId = "5511999990002",
            FarmaciaId = 1,
            Farmacia = null!,
            PerfilCompra = ClientePerfilCompraEnum.Prospecto
        };

        _mockCrmService
            .Setup(s => s.SearchClientes("1", It.IsAny<ClienteSearchFilter>()))
            .ReturnsAsync(new List<Cliente>());
        _mockCrmService
            .Setup(s => s.CreateCliente(It.IsAny<Cliente>()))
            .ReturnsAsync(createdCliente);

        // Act
        var result = await _controller.CreateCliente(cliente);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
        var created = result as CreatedAtActionResult;
        created!.StatusCode.Should().Be(201);

        // Verify PerfilCompra = Prospecto
        _mockCrmService.Verify(s => s.CreateCliente(
            It.Is<Cliente>(c => c.PerfilCompra == ClientePerfilCompraEnum.Prospecto)),
            Times.Once);
    }
}
