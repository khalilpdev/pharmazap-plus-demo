using FluentAssertions;
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
using PharmazapPlus.Shared.Models.Mensagens;
using PharmazapPlus.Shared.Services;
using System.Security.Claims;

namespace PharmazapPlus.API.Tests.Controllers;

public class MotivosEncerramentoControllerTests
{
    private readonly Mock<ILogger<MotivosEncerramentoController>> _mockLogger;
    private readonly PedidoService _pedidoService;
    private readonly PharmazapDbContext _context;
    private readonly Farmacia _farmacia;

    public MotivosEncerramentoControllerTests()
    {
        _mockLogger = new Mock<ILogger<MotivosEncerramentoController>>();
        _pedidoService = new PedidoService();

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
                ChaveWhatsApp = "test-key-motivos",
                WhatsAppPhoneNumberId = "",
                WhatsAppAccessToken = "",
                WhatsAppBusinessAccountId = ""
            },
        };
        _context.Farmacias.Add(_farmacia);
        _context.SaveChanges();
    }

    private MotivosEncerramentoController CreateControllerWithUser(string role, int farmaciaId)
    {
        var controller = new MotivosEncerramentoController(_context, _mockLogger.Object);
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

    private MotivoEncerramento CreateMotivo(string nome, bool exigeValor = false, bool exigeComprovante = false, bool ativo = true, int ordem = 0)
    {
        var motivo = new MotivoEncerramento
        {
            FarmaciaId = _farmacia.Id,
            Farmacia = null!,
            Nome = nome,
            ExigeValorVenda = exigeValor,
            ExigeComprovante = exigeComprovante,
            Ativo = ativo,
            Ordem = ordem
        };
        _context.MotivosEncerramento.Add(motivo);
        _context.SaveChanges();
        return motivo;
    }

    // ── GET /api/motivos-encerramento ──

    [Fact]
    public async Task GetMotivosEncerramento_ReturnsAll_WhenNoFilter()
    {
        CreateMotivo("Venda confirmada", exigeValor: true, exigeComprovante: true, ordem: 1);
        CreateMotivo("Cliente desistiu", ordem: 2);
        CreateMotivo("Inativo", ativo: false, ordem: 3);

        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var result = await controller.GetMotivosEncerramento();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var data = GetDynamicList(ok.Value, "Data");
        data.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetMotivosEncerramento_FiltersAtivos()
    {
        CreateMotivo("Ativo1", ativo: true);
        CreateMotivo("Inativo1", ativo: false);

        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var result = await controller.GetMotivosEncerramento(ativo: true);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var data = GetDynamicList(ok.Value, "Data");
        data.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetMotivosEncerramento_ReturnsBadRequest_WhenNoFarmacia()
    {
        var controller = CreateControllerWithUser(UserRoles.Administrador, 0);
        var result = await controller.GetMotivosEncerramento();
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── GET /api/motivos-encerramento/{id} ──

    [Fact]
    public async Task GetMotivoEncerramento_ReturnsMotivo()
    {
        var motivo = CreateMotivo("Teste");

        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var result = await controller.GetMotivoEncerramento(motivo.Id);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetMotivoEncerramento_ReturnsNotFound_WhenNotExists()
    {
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var result = await controller.GetMotivoEncerramento(999);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── POST /api/motivos-encerramento ──

    [Fact]
    public async Task CreateMotivoEncerramento_CreatesSuccessfully()
    {
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var request = new CreateMotivoEncerramentoRequest("Venda confirmada", true, true, true, 1);
        var result = await controller.CreateMotivoEncerramento(request);

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.StatusCode.Should().Be(201);

        var motivo = await _context.MotivosEncerramento.FirstOrDefaultAsync(m => m.Nome == "Venda confirmada");
        motivo.Should().NotBeNull();
        motivo!.ExigeValorVenda.Should().BeTrue();
        motivo.ExigeComprovante.Should().BeTrue();
    }

    [Fact]
    public async Task CreateMotivoEncerramento_ReturnsBadRequest_WhenNameEmpty()
    {
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var request = new CreateMotivoEncerramentoRequest("", null, null, null, null);
        var result = await controller.CreateMotivoEncerramento(request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateMotivoEncerramento_ReturnsBadRequest_WhenDuplicate()
    {
        CreateMotivo("Duplicado");

        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var request = new CreateMotivoEncerramentoRequest("Duplicado", null, null, null, null);
        var result = await controller.CreateMotivoEncerramento(request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── PUT /api/motivos-encerramento/{id} ──

    [Fact]
    public async Task UpdateMotivoEncerramento_UpdatesFields()
    {
        var motivo = CreateMotivo("Original", exigeValor: false);

        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var request = new UpdateMotivoEncerramentoRequest("Renomeado", true, true, null, 5);
        var result = await controller.UpdateMotivoEncerramento(motivo.Id, request);

        result.Should().BeOfType<OkObjectResult>();

        await _context.Entry(motivo).ReloadAsync();
        motivo.Nome.Should().Be("Renomeado");
        motivo.ExigeValorVenda.Should().BeTrue();
        motivo.ExigeComprovante.Should().BeTrue();
        motivo.Ordem.Should().Be(5);
    }

    [Fact]
    public async Task UpdateMotivoEncerramento_ReturnsNotFound_WhenNotExists()
    {
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var request = new UpdateMotivoEncerramentoRequest("Nope", null, null, null, null);
        var result = await controller.UpdateMotivoEncerramento(999, request);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateMotivoEncerramento_ReturnsBadRequest_WhenDuplicate()
    {
        CreateMotivo("Motivo A");
        var motivo = CreateMotivo("Motivo B");

        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var request = new UpdateMotivoEncerramentoRequest("Motivo A", null, null, null, null);
        var result = await controller.UpdateMotivoEncerramento(motivo.Id, request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── DELETE /api/motivos-encerramento/{id} ──

    [Fact]
    public async Task InativarMotivoEncerramento_SetsAtivoFalse()
    {
        var motivo = CreateMotivo("Para Inativar", ativo: true);

        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var result = await controller.InativarMotivoEncerramento(motivo.Id);

        result.Should().BeOfType<OkObjectResult>();

        await _context.Entry(motivo).ReloadAsync();
        motivo.Ativo.Should().BeFalse();
    }

    [Fact]
    public async Task InativarMotivoEncerramento_ReturnsNotFound_WhenNotExists()
    {
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var result = await controller.InativarMotivoEncerramento(999);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── PUT /api/motivos-encerramento/{id}/ativar ──

    [Fact]
    public async Task AtivarMotivoEncerramento_SetsAtivoTrue()
    {
        var motivo = CreateMotivo("Para Ativar", ativo: false);

        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var result = await controller.AtivarMotivoEncerramento(motivo.Id);

        result.Should().BeOfType<OkObjectResult>();

        await _context.Entry(motivo).ReloadAsync();
        motivo.Ativo.Should().BeTrue();
    }

    [Fact]
    public async Task AtivarMotivoEncerramento_ReturnsNotFound_WhenNotExists()
    {
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var result = await controller.AtivarMotivoEncerramento(999);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── Atendimento Finalização with Motivo Validation ──

    [Fact]
    public async Task Finalizar_WithMotivo_RequiresValorVenda()
    {
        // Create motivo that requires valor venda
        var motivo = CreateMotivo("Venda confirmada", exigeValor: true, exigeComprovante: false);

        // Create atendimento
        var atendente = new UsuarioFarmacia
        {
            Nome = "Atendente",
            Email = "atendente@teste.com",
            Telefone = "(11) 99999-9999",
            Role = UserRoles.Atendente,
            PasswordHash = "hash",
            PasswordSalt = "salt",
            Turno = "Manhã",
            FarmaciaId = _farmacia.Id,
            Farmacia = null!,
            Ativo = true,
            DataCadastro = DateTime.UtcNow
        };
        _context.UsuariosFarmacia.Add(atendente);
        _context.SaveChanges();

        var atendimento = new Atendimento
        {
            FarmaciaId = _farmacia.Id,
            Farmacia = null!,
            Cliente = null!,
            Atendente = null!,
            WhatsAppId = "+5511999999999",
            CanalEnum = Shared.Enums.CanalEnum.WhatsApp,
            StatusEnum = Shared.Enums.AtendimentoStatusEnum.EmAndamento,
            AtendenteId = atendente.Id,
            DataInicio = DateTime.UtcNow
        };
        _context.Atendimentos.Add(atendimento);
        _context.SaveChanges();

        // Create AtendimentosController
        var mockLogger = new Mock<ILogger<AtendimentosController>>();
        var mockWhatsApp = new Mock<PharmazapPlus.API.Services.WhatsApp.IWhatsAppService>();
        var mockHubContext = new Mock<Microsoft.AspNetCore.SignalR.IHubContext<PharmazapPlus.API.Hubs.PharmaHub>>();
        var mockClients = new Mock<Microsoft.AspNetCore.SignalR.IHubClients>();
        var mockClientProxy = new Mock<Microsoft.AspNetCore.SignalR.IClientProxy>();
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);
        mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);

        var mockRemetenteBloqueado = new Mock<IRemetenteBloqueadoService>();
        var controller = new AtendimentosController(_context, mockLogger.Object, mockWhatsApp.Object, mockHubContext.Object, _pedidoService, mockRemetenteBloqueado.Object);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, atendente.Id.ToString()),
            new Claim(ClaimTypes.Role, UserRoles.Atendente),
            new Claim("FarmaciaId", _farmacia.Id.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        // Try to finalize without valor venda — should fail
        var request = new AtendimentosController.FinalizarAtendimentoRequest(
            Motivo: null, ValorVenda: null, MotivoEncerramentoId: motivo.Id, ComprovanteUrl: null);
        var result = await controller.Finalizar(atendimento.Id, request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Finalizar_WithMotivo_RequiresComprovante()
    {
        var motivo = CreateMotivo("Venda com comprovante", exigeValor: false, exigeComprovante: true);

        var atendente = new UsuarioFarmacia
        {
            Nome = "Atendente",
            Email = "atendente2@teste.com",
            Telefone = "(11) 99999-9998",
            Role = UserRoles.Atendente,
            PasswordHash = "hash",
            PasswordSalt = "salt",
            Turno = "Manhã",
            FarmaciaId = _farmacia.Id,
            Farmacia = null!,
            Ativo = true,
            DataCadastro = DateTime.UtcNow
        };
        _context.UsuariosFarmacia.Add(atendente);
        _context.SaveChanges();

        var atendimento = new Atendimento
        {
            FarmaciaId = _farmacia.Id,
            Farmacia = null!,
            Cliente = null!,
            Atendente = null!,
            WhatsAppId = "+5511999999999",
            CanalEnum = Shared.Enums.CanalEnum.WhatsApp,
            StatusEnum = Shared.Enums.AtendimentoStatusEnum.EmAndamento,
            AtendenteId = atendente.Id,
            DataInicio = DateTime.UtcNow
        };
        _context.Atendimentos.Add(atendimento);
        _context.SaveChanges();

        var mockLogger = new Mock<ILogger<AtendimentosController>>();
        var mockWhatsApp = new Mock<PharmazapPlus.API.Services.WhatsApp.IWhatsAppService>();
        var mockHubContext = new Mock<Microsoft.AspNetCore.SignalR.IHubContext<PharmazapPlus.API.Hubs.PharmaHub>>();
        var mockClients = new Mock<Microsoft.AspNetCore.SignalR.IHubClients>();
        var mockClientProxy = new Mock<Microsoft.AspNetCore.SignalR.IClientProxy>();
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);
        mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);

        var mockRemetenteBloqueado = new Mock<IRemetenteBloqueadoService>();
        var controller = new AtendimentosController(_context, mockLogger.Object, mockWhatsApp.Object, mockHubContext.Object, _pedidoService, mockRemetenteBloqueado.Object);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, atendente.Id.ToString()),
            new Claim(ClaimTypes.Role, UserRoles.Atendente),
            new Claim("FarmaciaId", _farmacia.Id.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        // Try to finalize without comprovante — should fail
        var request = new AtendimentosController.FinalizarAtendimentoRequest(
            Motivo: null, ValorVenda: 100.50m, MotivoEncerramentoId: motivo.Id, ComprovanteUrl: null);
        var result = await controller.Finalizar(atendimento.Id, request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Finalizar_WhenAssignedToAnotherAtendente_ReturnsForbidden()
    {
        var atendenteResponsavel = new UsuarioFarmacia
        {
            Nome = "Atendente Responsável",
            Email = "responsavel@teste.com",
            Telefone = "(11) 99999-1111",
            Role = UserRoles.Atendente,
            PasswordHash = "hash",
            PasswordSalt = "salt",
            Turno = "Manhã",
            FarmaciaId = _farmacia.Id,
            Farmacia = null!,
            Ativo = true,
            DataCadastro = DateTime.UtcNow
        };
        var outroAtendente = new UsuarioFarmacia
        {
            Nome = "Outro Atendente",
            Email = "outro@teste.com",
            Telefone = "(11) 99999-2222",
            Role = UserRoles.Atendente,
            PasswordHash = "hash",
            PasswordSalt = "salt",
            Turno = "Tarde",
            FarmaciaId = _farmacia.Id,
            Farmacia = null!,
            Ativo = true,
            DataCadastro = DateTime.UtcNow
        };
        _context.UsuariosFarmacia.AddRange(atendenteResponsavel, outroAtendente);
        _context.SaveChanges();

        var atendimento = new Atendimento
        {
            FarmaciaId = _farmacia.Id,
            Farmacia = null!,
            Cliente = null!,
            Atendente = null!,
            WhatsAppId = "+5511999999999",
            CanalEnum = Shared.Enums.CanalEnum.WhatsApp,
            StatusEnum = Shared.Enums.AtendimentoStatusEnum.EmAndamento,
            AtendenteId = atendenteResponsavel.Id,
            DataInicio = DateTime.UtcNow
        };
        _context.Atendimentos.Add(atendimento);
        _context.SaveChanges();

        var mockLogger = new Mock<ILogger<AtendimentosController>>();
        var mockWhatsApp = new Mock<PharmazapPlus.API.Services.WhatsApp.IWhatsAppService>();
        var mockHubContext = new Mock<Microsoft.AspNetCore.SignalR.IHubContext<PharmazapPlus.API.Hubs.PharmaHub>>();
        var mockClients = new Mock<Microsoft.AspNetCore.SignalR.IHubClients>();
        var mockClientProxy = new Mock<Microsoft.AspNetCore.SignalR.IClientProxy>();
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);
        mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);

        var mockRemetenteBloqueado = new Mock<IRemetenteBloqueadoService>();
        var controller = new AtendimentosController(_context, mockLogger.Object, mockWhatsApp.Object, mockHubContext.Object, _pedidoService, mockRemetenteBloqueado.Object);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, outroAtendente.Id.ToString()),
            new Claim(ClaimTypes.Role, UserRoles.Atendente),
            new Claim("FarmaciaId", _farmacia.Id.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        var result = await controller.Finalizar(atendimento.Id, null);

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task Finalizar_WhenAdministradorNotAssigned_ReturnsForbidden()
    {
        var atendenteResponsavel = new UsuarioFarmacia
        {
            Nome = "Atendente Responsável",
            Email = "responsavel-admin@teste.com",
            Telefone = "(11) 99999-3333",
            Role = UserRoles.Atendente,
            PasswordHash = "hash",
            PasswordSalt = "salt",
            Turno = "Manhã",
            FarmaciaId = _farmacia.Id,
            Farmacia = null!,
            Ativo = true,
            DataCadastro = DateTime.UtcNow
        };
        _context.UsuariosFarmacia.Add(atendenteResponsavel);
        _context.SaveChanges();

        var atendimento = new Atendimento
        {
            FarmaciaId = _farmacia.Id,
            Farmacia = null!,
            Cliente = null!,
            Atendente = null!,
            WhatsAppId = "+5511999999999",
            CanalEnum = Shared.Enums.CanalEnum.WhatsApp,
            StatusEnum = Shared.Enums.AtendimentoStatusEnum.EmAndamento,
            AtendenteId = atendenteResponsavel.Id,
            DataInicio = DateTime.UtcNow
        };
        _context.Atendimentos.Add(atendimento);
        _context.SaveChanges();

        var mockLogger = new Mock<ILogger<AtendimentosController>>();
        var mockWhatsApp = new Mock<PharmazapPlus.API.Services.WhatsApp.IWhatsAppService>();
        var mockHubContext = new Mock<Microsoft.AspNetCore.SignalR.IHubContext<PharmazapPlus.API.Hubs.PharmaHub>>();
        var mockClients = new Mock<Microsoft.AspNetCore.SignalR.IHubClients>();
        var mockClientProxy = new Mock<Microsoft.AspNetCore.SignalR.IClientProxy>();
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);
        mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);

        var mockRemetenteBloqueado = new Mock<IRemetenteBloqueadoService>();
        var controller = new AtendimentosController(_context, mockLogger.Object, mockWhatsApp.Object, mockHubContext.Object, _pedidoService, mockRemetenteBloqueado.Object);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "999"),
            new Claim(ClaimTypes.Role, UserRoles.Administrador),
            new Claim("FarmaciaId", _farmacia.Id.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        var result = await controller.Finalizar(atendimento.Id, null);

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task Finalizar_WithMotivo_SucceedsWhenAllRulesMet()
    {
        var motivo = CreateMotivo("Venda completa", exigeValor: true, exigeComprovante: true);

        var atendente = new UsuarioFarmacia
        {
            Nome = "Atendente",
            Email = "atendente3@teste.com",
            Telefone = "(11) 99999-9997",
            Role = UserRoles.Atendente,
            PasswordHash = "hash",
            PasswordSalt = "salt",
            Turno = "Manhã",
            FarmaciaId = _farmacia.Id,
            Farmacia = null!,
            Ativo = true,
            DataCadastro = DateTime.UtcNow
        };
        _context.UsuariosFarmacia.Add(atendente);
        _context.SaveChanges();

        var atendimento = new Atendimento
        {
            FarmaciaId = _farmacia.Id,
            Farmacia = null!,
            Cliente = null!,
            Atendente = null!,
            WhatsAppId = "+5511999999999",
            CanalEnum = Shared.Enums.CanalEnum.WhatsApp,
            StatusEnum = Shared.Enums.AtendimentoStatusEnum.EmAndamento,
            AtendenteId = atendente.Id,
            DataInicio = DateTime.UtcNow
        };
        _context.Atendimentos.Add(atendimento);
        _context.SaveChanges();

        var mockLogger = new Mock<ILogger<AtendimentosController>>();
        var mockWhatsApp = new Mock<PharmazapPlus.API.Services.WhatsApp.IWhatsAppService>();
        var mockHubContext = new Mock<Microsoft.AspNetCore.SignalR.IHubContext<PharmazapPlus.API.Hubs.PharmaHub>>();
        var mockClients = new Mock<Microsoft.AspNetCore.SignalR.IHubClients>();
        var mockClientProxy = new Mock<Microsoft.AspNetCore.SignalR.IClientProxy>();
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);
        mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);

        var mockRemetenteBloqueado = new Mock<IRemetenteBloqueadoService>();
        var controller = new AtendimentosController(_context, mockLogger.Object, mockWhatsApp.Object, mockHubContext.Object, _pedidoService, mockRemetenteBloqueado.Object);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, atendente.Id.ToString()),
            new Claim(ClaimTypes.Role, UserRoles.Atendente),
            new Claim("FarmaciaId", _farmacia.Id.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        var request = new AtendimentosController.FinalizarAtendimentoRequest(
            Motivo: null, ValorVenda: 250.00m, MotivoEncerramentoId: motivo.Id, ComprovanteUrl: "/uploads/comprovante.jpg");
        var result = await controller.Finalizar(atendimento.Id, request);

        result.Should().BeOfType<OkObjectResult>();

        await _context.Entry(atendimento).ReloadAsync();
        atendimento.StatusEnum.Should().Be(Shared.Enums.AtendimentoStatusEnum.Finalizado);
        atendimento.MotivoEncerramentoId.Should().Be(motivo.Id);
        atendimento.ValorVenda.Should().Be(250.00m);
        atendimento.ComprovanteUrl.Should().Be("/uploads/comprovante.jpg");
    }

    // Helpers

    private static List<dynamic> GetDynamicList(object? obj, string propertyName)
    {
        if (obj == null) return new List<dynamic>();
        var prop = obj.GetType().GetProperty(propertyName);
        if (prop == null) return new List<dynamic>();
        var enumerable = prop.GetValue(obj) as System.Collections.IEnumerable;
        return enumerable?.Cast<dynamic>().ToList() ?? new List<dynamic>();
    }
}
