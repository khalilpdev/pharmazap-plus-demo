using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using PharmazapPlus.API.Controllers;
using PharmazapPlus.API.Data;
using PharmazapPlus.Shared.Models.Core;
using PharmazapPlus.Shared.Models.CRM;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PharmazapPlus.Shared;

namespace PharmazapPlus.API.Tests.Controllers;

public class DepartamentosControllerTests
{
    private readonly Mock<ILogger<DepartamentosController>> _mockLogger;
    private readonly PharmazapDbContext _context;
    private readonly Farmacia _farmacia;

    public DepartamentosControllerTests()
    {
        _mockLogger = new Mock<ILogger<DepartamentosController>>();

        var options = new DbContextOptionsBuilder<PharmazapDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new PharmazapDbContext(options);

        _farmacia = new Farmacia
        {
            Nome = "Farmacia Teste",
            Cnpj = "00.000.000/0001-01",
            Telefone = "(11) 99999-9999",
            Email = "contato@teste.com",
            Endereco = "Rua Teste, 123",
            Cidade = "São Paulo",
            Estado = "SP",
            Cep = "01000000",
            DataCadastro = DateTime.UtcNow,
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "test-key-departamentos",
                WhatsAppPhoneNumberId = "",
                WhatsAppAccessToken = "",
                WhatsAppBusinessAccountId = ""
            },
        };

        _context.Farmacias.Add(_farmacia);
        _context.SaveChanges();
    }

    private DepartamentosController CreateControllerWithUser(string role, int farmaciaId)
    {
        var controller = new DepartamentosController(_context, _mockLogger.Object);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Email, "admin@teste.com"),
            new Claim(ClaimTypes.Name, "Admin User"),
            new Claim(ClaimTypes.Role, role),
            new Claim("FarmaciaId", farmaciaId.ToString()),
            new Claim("FarmaciaNome", "Farmacia Teste")
        };

        var identity = new ClaimsIdentity(claims, "TestAuthType");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    private DepartamentosController CreateControllerWithoutFarmacia()
    {
        var controller = new DepartamentosController(_context, _mockLogger.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return controller;
    }

    private Departamento CreateTestDepartamento(string nome, int farmaciaId, bool ativo = true)
    {
        var departamento = new Departamento
        {
            Nome = nome,
            Ativo = ativo,
            FarmaciaId = farmaciaId,
            Farmacia = null
        };

        _context.Departamentos.Add(departamento);
        _context.SaveChanges();
        return departamento;
    }

    [Fact]
    public async Task GetDepartamentos_WithValidFarmacia_ReturnsDepartamentoList()
    {
        CreateTestDepartamento("Atendente", _farmacia.Id);
        CreateTestDepartamento("Financeiro", _farmacia.Id);
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);

        var result = await controller.GetDepartamentos();

        result.Should().BeOfType<OkObjectResult>();
        (result as OkObjectResult)!.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task CreateDepartamento_WithValidData_ReturnsCreated()
    {
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var request = new CreateDepartamentoRequest("Comercial", true);

        var result = await controller.CreateDepartamento(request);

        result.Should().BeOfType<CreatedAtActionResult>();
        (result as CreatedAtActionResult)!.StatusCode.Should().Be(201);
    }

    [Fact]
    public async Task CreateDepartamento_WithDuplicateName_ReturnsBadRequest()
    {
        CreateTestDepartamento("Atendente", _farmacia.Id);
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);

        var result = await controller.CreateDepartamento(new CreateDepartamentoRequest("Atendente", true));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateDepartamento_WithoutFarmaciaId_ReturnsBadRequest()
    {
        var controller = CreateControllerWithoutFarmacia();

        var result = await controller.CreateDepartamento(new CreateDepartamentoRequest("Atendente", true));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateDepartamento_WithValidData_ReturnsOk()
    {
        var departamento = CreateTestDepartamento("Atendente", _farmacia.Id);
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);

        var result = await controller.UpdateDepartamento(departamento.Id, new UpdateDepartamentoRequest("Atendimento", null));

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task InativarDepartamento_WithValidId_ReturnsOk()
    {
        var departamento = CreateTestDepartamento("Atendente", _farmacia.Id);
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);

        var result = await controller.InativarDepartamento(departamento.Id);

        result.Should().BeOfType<OkObjectResult>();
        (await _context.Departamentos.FindAsync(departamento.Id))!.Ativo.Should().BeFalse();
    }

    [Fact]
    public async Task AtivarDepartamento_WithInactiveDepartamento_ReturnsOk()
    {
        var departamento = CreateTestDepartamento("Atendente", _farmacia.Id, ativo: false);
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);

        var result = await controller.AtivarDepartamento(departamento.Id);

        result.Should().BeOfType<OkObjectResult>();
        (await _context.Departamentos.FindAsync(departamento.Id))!.Ativo.Should().BeTrue();
    }
}
