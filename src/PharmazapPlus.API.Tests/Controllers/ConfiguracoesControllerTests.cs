using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using PharmazapPlus.API.Controllers;
using PharmazapPlus.API.Data;
using PharmazapPlus.API.Services.WhatsApp;
using PharmazapPlus.Shared;
using PharmazapPlus.Shared.Models.Core;
using System.Security.Claims;
using System.Text.Json;

namespace PharmazapPlus.API.Tests.Controllers;

public class ConfiguracoesControllerTests
{
    private readonly Mock<ILogger<ConfiguracoesController>> _mockLogger;
    private readonly Mock<IRemetenteBloqueadoService> _mockRemetenteBloqueado;
    private readonly PharmazapDbContext _context;
    private readonly Farmacia _farmacia;

    public ConfiguracoesControllerTests()
    {
        _mockLogger = new Mock<ILogger<ConfiguracoesController>>();
        _mockRemetenteBloqueado = new Mock<IRemetenteBloqueadoService>();

        var options = new DbContextOptionsBuilder<PharmazapDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new PharmazapDbContext(options);

        _farmacia = new Farmacia
        {
            Nome = "Farmácia Config",
            Cnpj = "00.000.000/0001-01",
            Telefone = "(11) 99999-9999",
            Email = "config@teste.com",
            Endereco = "Rua Teste, 123",
            Cidade = "São Paulo",
            Estado = "SP",
            Cep = "01000000",
            DataCadastro = DateTime.UtcNow,
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "wa-key",
                SalvarLogCompletoMidiaApiGo = false
            }
        };

        _context.Farmacias.Add(_farmacia);
        _context.SaveChanges();
    }

    private ConfiguracoesController CreateControllerWithUser(string role, int farmaciaId)
    {
        var controller = new ConfiguracoesController(_context, _mockLogger.Object, _mockRemetenteBloqueado.Object);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "1"),
            new(ClaimTypes.Role, role),
            new("FarmaciaId", farmaciaId.ToString())
        };

        var identity = new ClaimsIdentity(claims, "TestAuthType");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        return controller;
    }

    [Fact]
    public void ConfiguracoesController_RequiresGestorAdministradorOuDesenvolvedor()
    {
        var authorize = typeof(ConfiguracoesController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .OfType<AuthorizeAttribute>()
            .Single();

        authorize.Roles.Should().Be($"{UserRoles.Gestor},{UserRoles.Administrador},{UserRoles.Desenvolvedor}");
        authorize.Roles.Should().NotContain(UserRoles.Atendente);
    }

    [Fact]
    public async Task GetSalvarLogCompletoMidiaApiGo_ReturnsCurrentValue()
    {
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);

        var result = await controller.GetSalvarLogCompletoMidiaApiGo();

        result.Should().BeOfType<OkObjectResult>();
        var payload = ((OkObjectResult)result).Value;
        payload.Should().NotBeNull();

        var json = JsonSerializer.Serialize(payload);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("data").GetProperty("salvarLogCompletoMidiaApiGo").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task UpdateSalvarLogCompletoMidiaApiGo_PersistsValue()
    {
        var controller = CreateControllerWithUser(UserRoles.Gestor, _farmacia.Id);

        var result = await controller.UpdateSalvarLogCompletoMidiaApiGo(new UpdateSalvarLogCompletoMidiaApiGoRequest(true));

        result.Should().BeOfType<OkObjectResult>();

        var farmaciaAtualizada = await _context.Farmacias
            .Include(f => f.Integracoes)
            .FirstAsync(f => f.Id == _farmacia.Id);

        farmaciaAtualizada.Integracoes.Should().NotBeNull();
        farmaciaAtualizada.Integracoes!.SalvarLogCompletoMidiaApiGo.Should().BeTrue();
    }

    [Fact]
    public async Task GetHabilitarBloqueioRemetente_ReturnsCurrentValue()
    {
        _farmacia.Integracoes!.HabilitarBloqueioRemetente = true;
        await _context.SaveChangesAsync();

        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);

        var result = await controller.GetHabilitarBloqueioRemetente();

        result.Should().BeOfType<OkObjectResult>();
        var payload = ((OkObjectResult)result).Value;
        payload.Should().NotBeNull();

        var json = JsonSerializer.Serialize(payload);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("habilitarBloqueioRemetente").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task UpdateHabilitarBloqueioRemetente_PersistsValue()
    {
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);

        var result = await controller.UpdateHabilitarBloqueioRemetente(new UpdateHabilitarBloqueioRemetenteRequest(true));

        result.Should().BeOfType<OkObjectResult>();

        var farmaciaAtualizada = await _context.Farmacias
            .Include(f => f.Integracoes)
            .FirstAsync(f => f.Id == _farmacia.Id);

        farmaciaAtualizada.Integracoes.Should().NotBeNull();
        farmaciaAtualizada.Integracoes!.HabilitarBloqueioRemetente.Should().BeTrue();
    }

    [Fact]
    public async Task GetPermitirGerenciarEtiquetasAtendente_ReturnsCurrentValue()
    {
        _farmacia.Integracoes!.PermitirGerenciarEtiquetasAtendente = true;
        await _context.SaveChangesAsync();

        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);

        var result = await controller.GetPermitirGerenciarEtiquetasAtendente();

        result.Should().BeOfType<OkObjectResult>();
        var payload = ((OkObjectResult)result).Value;
        payload.Should().NotBeNull();

        var json = JsonSerializer.Serialize(payload);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("permitirGerenciarEtiquetasAtendente").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task UpdatePermitirGerenciarEtiquetasAtendente_PersistsValue()
    {
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);

        var result = await controller.UpdatePermitirGerenciarEtiquetasAtendente(new UpdatePermitirGerenciarEtiquetasAtendenteRequest(true));

        result.Should().BeOfType<OkObjectResult>();

        var farmaciaAtualizada = await _context.Farmacias
            .Include(f => f.Integracoes)
            .FirstAsync(f => f.Id == _farmacia.Id);

        farmaciaAtualizada.Integracoes.Should().NotBeNull();
        farmaciaAtualizada.Integracoes!.PermitirGerenciarEtiquetasAtendente.Should().BeTrue();
    }
}
