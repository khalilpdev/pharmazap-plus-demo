using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using PharmazapPlus.API.Controllers;
using PharmazapPlus.API.Services.CRM;
using PharmazapPlus.Shared.Models.CRM;
using System.Security.Claims;
using FluentAssertions;

namespace PharmazapPlus.API.Tests.Controllers;

/// <summary>
/// Testes de autorização multi-tenant por FarmaciaId.
/// Garante que operações são isoladas por farmácia e que dados de um tenant
/// não são acessíveis por outro.
/// </summary>
public class MultiTenantAuthorizationTests
{
    private readonly Mock<ICrmService> _mockCrmService;
    private readonly Mock<ILogger<ClientesController>> _mockLogger;

    public MultiTenantAuthorizationTests()
    {
        _mockCrmService = new Mock<ICrmService>();
        _mockLogger = new Mock<ILogger<ClientesController>>();
    }

    private ClientesController CreateControllerWithFarmacia(string farmaciaId)
    {
        var controller = new ClientesController(_mockCrmService.Object, _mockLogger.Object);
        var claims = new List<Claim>
        {
            new Claim("FarmaciaId", farmaciaId),
            new Claim("FarmaciaNome", $"Farmacia {farmaciaId}")
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
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

    // ── GET /api/clientes — sem FarmaciaId retorna 400 ───────────────

    [Fact]
    public async Task GetClientes_WithoutFarmaciaId_ReturnsBadRequest()
    {
        var controller = CreateControllerWithoutFarmacia();

        var result = await controller.GetClientes();

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── GET /api/clientes/{id} — FarmaciaId isolado ──────────────────

    [Fact]
    public async Task GetCliente_WithValidFarmacia_PassesFarmaciaIdToService()
    {
        // Arrange
        var clienteFarmacia1 = new Cliente { Id = 1, Nome = "Cliente F1", FarmaciaId = 1, WhatsAppId = "test1", Farmacia = null! };
        _mockCrmService
            .Setup(s => s.GetCliente(1, "1"))
            .ReturnsAsync(clienteFarmacia1);

        var controller = CreateControllerWithFarmacia("1");

        // Act
        var result = await controller.GetCliente(1);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockCrmService.Verify(s => s.GetCliente(1, "1"), Times.Once);
    }

    [Fact]
    public async Task GetCliente_ServiceThrowsForWrongTenant_ReturnsNotFound()
    {
        // Arrange — serviço lança exceção quando o cliente não pertence à farmácia
        _mockCrmService
            .Setup(s => s.GetCliente(99, "2"))
            .ThrowsAsync(new NotFoundException("Cliente 99 não encontrado"));

        var controller = CreateControllerWithFarmacia("2");

        // Act
        var result = await controller.GetCliente(99);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetCliente_WithoutFarmaciaId_ReturnsBadRequest()
    {
        var controller = CreateControllerWithoutFarmacia();

        var result = await controller.GetCliente(1);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── POST /api/clientes — FarmaciaId no claim, não no body ────────

    [Fact]
    public async Task CreateCliente_WithoutFarmaciaId_ReturnsBadRequest()
    {
        var controller = CreateControllerWithoutFarmacia();
        var cliente = new Cliente { Nome = "Novo Cliente", WhatsAppId = "5511999999999", Farmacia = null! };

        var result = await controller.CreateCliente(cliente);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateCliente_ServiceAssignsFarmaciaIdFromClaim()
    {
        // Arrange
        var createdCliente = new Cliente { Id = 1, Nome = "Novo", WhatsAppId = "5511000000000", FarmaciaId = 3, Farmacia = null! };
        _mockCrmService
            .Setup(s => s.CreateCliente(It.IsAny<Cliente>()))
            .ReturnsAsync(createdCliente);

        var controller = CreateControllerWithFarmacia("3");
        var cliente = new Cliente { Nome = "Novo", WhatsAppId = "5511000000000", Farmacia = null! };

        // Act
        var result = await controller.CreateCliente(cliente);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
        // Verify FarmaciaId was assigned from claim before calling service
        _mockCrmService.Verify(s => s.CreateCliente(It.Is<Cliente>(c => c.FarmaciaId == 3)), Times.Once);
    }

    // ── PUT /api/clientes/{id} — FarmaciaId isolado ──────────────────

    [Fact]
    public async Task UpdateCliente_WithoutFarmaciaId_ReturnsBadRequest()
    {
        var controller = CreateControllerWithoutFarmacia();
        var dto = new ClienteUpdateDto { Nome = "Atualizado" };

        var result = await controller.UpdateCliente(1, dto);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateCliente_WithValidFarmacia_PassesFarmaciaIdToService()
    {
        // Arrange
        var existingCliente = new Cliente { Id = 1, Nome = "Original", FarmaciaId = 1, WhatsAppId = "test1", Farmacia = null! };
        var updatedCliente = new Cliente { Id = 1, Nome = "Atualizado", FarmaciaId = 1, WhatsAppId = "test1", Farmacia = null! };
        _mockCrmService
            .Setup(s => s.GetCliente(1, "1"))
            .ReturnsAsync(existingCliente);
        _mockCrmService
            .Setup(s => s.UpdateCliente(1, It.IsAny<ClienteUpdateDto>()))
            .ReturnsAsync(updatedCliente);

        var controller = CreateControllerWithFarmacia("1");
        var dto = new ClienteUpdateDto { Nome = "Atualizado" };

        // Act
        var result = await controller.UpdateCliente(1, dto);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockCrmService.Verify(s => s.GetCliente(1, "1"), Times.Once);
        _mockCrmService.Verify(s => s.UpdateCliente(1, It.IsAny<ClienteUpdateDto>()), Times.Once);
    }

    // ── DELETE /api/clientes/{id} — FarmaciaId isolado ───────────────

    [Fact]
    public async Task DeleteCliente_WithoutFarmaciaId_ReturnsBadRequest()
    {
        var controller = CreateControllerWithoutFarmacia();

        var result = await controller.DeleteCliente(1);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DeleteCliente_WithValidFarmacia_PassesFarmaciaIdToService()
    {
        // Arrange
        _mockCrmService
            .Setup(s => s.DeleteCliente(1, "1"))
            .ReturnsAsync((true, string.Empty));

        var controller = CreateControllerWithFarmacia("1");

        // Act
        var result = await controller.DeleteCliente(1);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockCrmService.Verify(s => s.DeleteCliente(1, "1"), Times.Once);
    }

    // ── Tenant isolation: FarmaciaId "1" não acessa dados de "2" ─────

    [Fact]
    public async Task GetClientes_DifferentFarmacias_CallServiceWithCorrectTenant()
    {
        // Arrange
        var clientesFarmacia1 = new List<Cliente> { new() { Id = 1, Nome = "C1", FarmaciaId = 1, WhatsAppId = "test1", Farmacia = null! } };
        var clientesFarmacia2 = new List<Cliente> { new() { Id = 2, Nome = "C2", FarmaciaId = 2, WhatsAppId = "test2", Farmacia = null! } };

        _mockCrmService
            .Setup(s => s.SearchClientes("1", It.IsAny<ClienteSearchFilter>()))
            .ReturnsAsync(clientesFarmacia1);
        _mockCrmService
            .Setup(s => s.SearchClientes("2", It.IsAny<ClienteSearchFilter>()))
            .ReturnsAsync(clientesFarmacia2);

        var controllerF1 = CreateControllerWithFarmacia("1");
        var controllerF2 = CreateControllerWithFarmacia("2");

        // Act
        var resultF1 = await controllerF1.GetClientes();
        var resultF2 = await controllerF2.GetClientes();

        // Assert — each controller only reaches its own tenant
        _mockCrmService.Verify(s => s.SearchClientes("1", It.IsAny<ClienteSearchFilter>()), Times.Once);
        _mockCrmService.Verify(s => s.SearchClientes("2", It.IsAny<ClienteSearchFilter>()), Times.Once);
        resultF1.Should().BeOfType<OkObjectResult>();
        resultF2.Should().BeOfType<OkObjectResult>();
    }

    // ── Observações — FarmaciaId required ────────────────────────────

    [Fact]
    public async Task AddObservacaoToCliente_WithoutFarmaciaId_ReturnsBadRequest()
    {
        var controller = CreateControllerWithoutFarmacia();
        var dto = new ClienteObservacaoDto
        {
            Titulo = "Alergia",
            Descricao = "Penicilina",
            Tipo = "Saude",
            Prioridade = "Alta",
            Responsavel = "Dr. João"
        };

        var result = await controller.AddObservacaoToCliente(1, dto);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
