using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using PharmazapPlus.API.Controllers;
using PharmazapPlus.API.Data;
using PharmazapPlus.API.Hubs;
using PharmazapPlus.API.Services.WhatsApp;
using PharmazapPlus.Shared;
using PharmazapPlus.Shared.Enums;
using PharmazapPlus.Shared.Models.Core;
using PharmazapPlus.Shared.Models.Mensagens;
using PharmazapPlus.Shared.Services;
using System.Security.Claims;

namespace PharmazapPlus.API.Tests.Controllers;

public class AtendimentosControllerTests
{
    private readonly Mock<ILogger<AtendimentosController>> _mockLogger;
    private readonly Mock<IWhatsAppService> _mockWhatsApp;
    private readonly Mock<IHubContext<PharmaHub>> _mockHubContext;
    private readonly Mock<IRemetenteBloqueadoService> _mockRemetenteBloqueado;
    private readonly PedidoService _pedidoService;
    private readonly PharmazapDbContext _context;
    private readonly Farmacia _farmacia;

    public AtendimentosControllerTests()
    {
        _mockLogger = new Mock<ILogger<AtendimentosController>>();
        _mockWhatsApp = new Mock<IWhatsAppService>();
        _mockRemetenteBloqueado = new Mock<IRemetenteBloqueadoService>();
        _pedidoService = new PedidoService();

        // Configure mock hub context (no-op by default)
        _mockHubContext = new Mock<IHubContext<PharmaHub>>();
        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);
        _mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);

        // Configure mock WhatsApp service to return success by default
        _mockWhatsApp
            .Setup(s => s.SendTextMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new WhatsAppMessageResponse
            {
                Success = true,
                MessageId = "wa-msg-001",
                Timestamp = DateTime.UtcNow
            });

        var options = new DbContextOptionsBuilder<PharmazapDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new PharmazapDbContext(options);

        _farmacia = new Farmacia
        {
            Nome = "Farmacia Teste",
            Cnpj = "12.345.678/0001-90",
            Telefone = "(11) 99999-9999",
            Email = "teste@farmacia.com",
            Endereco = "Rua Teste, 123",
            Cidade = "São Paulo",
            Estado = "SP",
            Cep = "01000000",
            DataCadastro = DateTime.UtcNow,
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "wa-key",
                WhatsAppPhoneNumberId = "phone-id",
                WhatsAppAccessToken = "access-token",
                WhatsAppBusinessAccountId = "biz-id"
            },
        };
        _context.Farmacias.Add(_farmacia);
        _context.SaveChanges();
    }

    private AtendimentosController CreateControllerWithUser(
        string role,
        int farmaciaId,
        string userId = "10")
    {
        var controller = new AtendimentosController(_context, _mockLogger.Object, _mockWhatsApp.Object, _mockHubContext.Object, _pedidoService, _mockRemetenteBloqueado.Object);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Email, "atendente@teste.com"),
            new Claim(ClaimTypes.Name, "Atendente Teste"),
            new Claim(ClaimTypes.Role, role),
            new Claim("FarmaciaId", farmaciaId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    private Atendimento CreateAtendimento(string status = "Pendente", int? atendenteId = null)
    {
        var statusEnum = Enum.TryParse<AtendimentoStatusEnum>(status, out var parsedStatus)
            ? parsedStatus
            : AtendimentoStatusEnum.Pendente;

        var atendimento = new Atendimento
        {
            FarmaciaId = _farmacia.Id,
            Farmacia = null!,
            Cliente = null!,
            Atendente = null!,
            WhatsAppId = "+5511999999999",
            CanalEnum = CanalEnum.WhatsApp,
            StatusEnum = statusEnum,
            AtendenteId = atendenteId,
            DataInicio = DateTime.UtcNow
        };
        _context.Atendimentos.Add(atendimento);
        _context.SaveChanges();
        return atendimento;
    }

    private Atendimento CreateGrupoVisualizacao(string status = "Pendente", int? atendenteId = null)
    {
        var atendimento = CreateAtendimento(status, atendenteId);
        atendimento.WhatsAppId = "120363044444@g.us";
        _context.SaveChanges();
        return atendimento;
    }

    private UsuarioFarmacia CreateAtendente(string nome, int farmaciaId)
    {
        var atendente = new UsuarioFarmacia
        {
            Nome = nome,
            Email = $"{nome.ToLower().Replace(" ", ".")}@teste.com",
            Telefone = "(11) 99999-9999",
            Role = UserRoles.Atendente,
            PasswordHash = "hash",
            PasswordSalt = "salt",
            Turno = "Manhã",
            FarmaciaId = farmaciaId,
            Farmacia = null!,
            Ativo = true,
            DataCadastro = DateTime.UtcNow
        };
        _context.UsuariosFarmacia.Add(atendente);
        _context.SaveChanges();
        return atendente;
    }

    // ─── GetAtendimentos ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAtendimentos_WithValidFarmacia_ReturnsAtendimentoList()
    {
        // Arrange
        CreateAtendimento(AtendimentoStatusEnum.Pendente.ToString());
        CreateAtendimento(AtendimentoStatusEnum.EmAndamento.ToString());
        var controller = CreateControllerWithUser(UserRoles.Atendente, _farmacia.Id);

        // Act
        var result = await controller.GetAtendimentos();

        // Assert
        var ok = result as OkObjectResult;
        ok.Should().NotBeNull();
        ok!.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task GetAtendimentos_FilterByStatus_ReturnsFilteredList()
    {
        // Arrange
        CreateAtendimento(AtendimentoStatusEnum.Pendente.ToString());
        CreateAtendimento(AtendimentoStatusEnum.EmAndamento.ToString());
        var controller = CreateControllerWithUser(UserRoles.Atendente, _farmacia.Id);

        // Act
        var result = await controller.GetAtendimentos(AtendimentoStatusEnum.Pendente.ToString());

        // Assert
        var ok = result as OkObjectResult;
        ok.Should().NotBeNull();
        ok!.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task GetAtendimentos_MissingFarmaciaId_ReturnsBadRequest()
    {
        // Arrange
        var controller = new AtendimentosController(_context, _mockLogger.Object, _mockWhatsApp.Object, _mockHubContext.Object, _pedidoService, _mockRemetenteBloqueado.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        // Act
        var result = await controller.GetAtendimentos();

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ─── GetMensagens ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMensagens_ExistingAtendimento_ReturnsMensagens()
    {
        // Arrange
        var atendimento = CreateAtendimento(AtendimentoStatusEnum.EmAndamento.ToString());
        var mensagem = new Mensagem
        {
            AtendimentoId = atendimento.Id,
            Direction = MessageDirection.Inbound,
            Type = MessageType.Text,
            Status = MessageStatus.Recebida,
            Text = "Olá, preciso de ajuda",
            ReceivedAt = DateTime.UtcNow
        };
        _context.Mensagens.Add(mensagem);
        await _context.SaveChangesAsync();

        var controller = CreateControllerWithUser(UserRoles.Atendente, _farmacia.Id);

        // Act
        var result = await controller.GetMensagens(atendimento.Id);

        // Assert
        var ok = result as OkObjectResult;
        ok.Should().NotBeNull();
        ok!.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task GetMensagens_NonExistentAtendimento_ReturnsNotFound()
    {
        // Arrange
        var controller = CreateControllerWithUser(UserRoles.Atendente, _farmacia.Id);

        // Act
        var result = await controller.GetMensagens(9999);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ─── Assumir ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Assumir_PendingAtendimento_ReturnsOkAndChangesStatus()
    {
        // Arrange
        var atendimento = CreateAtendimento(AtendimentoStatusEnum.Pendente.ToString());
        var controller = CreateControllerWithUser(UserRoles.Atendente, _farmacia.Id, userId: "42");

        // Act
        var result = await controller.Assumir(atendimento.Id);

        // Assert
        var ok = result as OkObjectResult;
        ok.Should().NotBeNull();
        ok!.StatusCode.Should().Be(200);

        var updated = await _context.Atendimentos.FindAsync(atendimento.Id);
        updated!.Status.Should().Be(AtendimentoStatusEnum.EmAndamento.ToString());
        updated.AtendenteId.Should().Be(42);
    }

    [Fact]
    public async Task Assumir_AlreadyActiveAtendimento_ReturnsBadRequest()
    {
        // Arrange
        var atendimento = CreateAtendimento(AtendimentoStatusEnum.EmAndamento.ToString());
        var controller = CreateControllerWithUser(UserRoles.Atendente, _farmacia.Id, userId: "42");

        // Act
        var result = await controller.Assumir(atendimento.Id);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Assumir_EmAndamentoByAdmin_ReturnsOkAndReassigns()
    {
        // Arrange
        var atendimento = CreateAtendimento(AtendimentoStatusEnum.EmAndamento.ToString());
        atendimento.AtendenteId = 99; // currently assigned to another attendant
        await _context.SaveChangesAsync();
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id, userId: "42");

        // Act
        var result = await controller.Assumir(atendimento.Id);

        // Assert
        var ok = result as OkObjectResult;
        ok.Should().NotBeNull();
        ok!.StatusCode.Should().Be(200);
        var updated = await _context.Atendimentos.FindAsync(atendimento.Id);
        updated!.AtendenteId.Should().Be(42);
    }

    [Fact]
    public async Task Assumir_EmAndamentoByGestor_ReturnsOkAndReassigns()
    {
        // Arrange
        var atendimento = CreateAtendimento(AtendimentoStatusEnum.EmAndamento.ToString());
        atendimento.AtendenteId = 99;
        await _context.SaveChangesAsync();
        var controller = CreateControllerWithUser(UserRoles.Gestor, _farmacia.Id, userId: "55");

        // Act
        var result = await controller.Assumir(atendimento.Id);

        // Assert
        var ok = result as OkObjectResult;
        ok.Should().NotBeNull();
        ok!.StatusCode.Should().Be(200);
        var updated = await _context.Atendimentos.FindAsync(atendimento.Id);
        updated!.AtendenteId.Should().Be(55);
    }

    [Fact]
    public async Task Assumir_NonExistentAtendimento_ReturnsNotFound()
    {
        // Arrange
        var controller = CreateControllerWithUser(UserRoles.Atendente, _farmacia.Id);

        // Act
        var result = await controller.Assumir(9999);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Assumir_WrongFarmacia_ReturnsNotFound()
    {
        // Arrange
        var atendimento = CreateAtendimento(AtendimentoStatusEnum.Pendente.ToString());
        var controller = CreateControllerWithUser(UserRoles.Atendente, farmaciaId: 9999, userId: "42");

        // Act
        var result = await controller.Assumir(atendimento.Id);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Assumir_GrupoVisualizacao_ReturnsBadRequestAndDoesNotAssign()
    {
        var atendimento = CreateGrupoVisualizacao(AtendimentoStatusEnum.Pendente.ToString());
        var controller = CreateControllerWithUser(UserRoles.Atendente, _farmacia.Id, userId: "42");

        var result = await controller.Assumir(atendimento.Id);

        result.Should().BeOfType<BadRequestObjectResult>();
        var updated = await _context.Atendimentos.FindAsync(atendimento.Id);
        updated!.StatusEnum.Should().Be(AtendimentoStatusEnum.Pendente);
        updated.AtendenteId.Should().BeNull();
    }

    // ─── EnviarMensagem ─────────────────────────────────────────────────────────

    [Fact]
    public async Task EnviarMensagem_ValidRequest_PersistsMensagemAndReturnsOk()
    {
        // Arrange — atendimento must be assigned to the same user (id=10) that sends the message
        var atendimento = CreateAtendimento(AtendimentoStatusEnum.EmAndamento.ToString(), atendenteId: 10);
        var controller = CreateControllerWithUser(UserRoles.Atendente, _farmacia.Id);
        var request = new AtendimentosController.EnviarMensagemRequest("Mensagem de teste");

        // Act
        var result = await controller.EnviarMensagem(atendimento.Id, request);

        // Assert
        var ok = result as OkObjectResult;
        ok.Should().NotBeNull();
        ok!.StatusCode.Should().Be(200);

        var mensagens = await _context.Mensagens
            .Where(m => m.AtendimentoId == atendimento.Id)
            .ToListAsync();
        mensagens.Should().HaveCount(1);
        mensagens[0].Text.Should().Be("Mensagem de teste");
        mensagens[0].Direction.Should().Be(MessageDirection.Outbound);
    }

    [Fact]
    public async Task EnviarMensagem_EmptyText_ReturnsBadRequest()
    {
        // Arrange
        var atendimento = CreateAtendimento(AtendimentoStatusEnum.EmAndamento.ToString());
        var controller = CreateControllerWithUser(UserRoles.Atendente, _farmacia.Id);
        var request = new AtendimentosController.EnviarMensagemRequest("   ");

        // Act
        var result = await controller.EnviarMensagem(atendimento.Id, request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task EnviarMensagem_PendingAtendimento_ReturnsBadRequest()
    {
        // Arrange
        var atendimento = CreateAtendimento(AtendimentoStatusEnum.Pendente.ToString());
        var controller = CreateControllerWithUser(UserRoles.Atendente, _farmacia.Id);
        var request = new AtendimentosController.EnviarMensagemRequest("Olá");

        // Act
        var result = await controller.EnviarMensagem(atendimento.Id, request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task EnviarMensagem_NonExistentAtendimento_ReturnsNotFound()
    {
        // Arrange
        var controller = CreateControllerWithUser(UserRoles.Atendente, _farmacia.Id);
        var request = new AtendimentosController.EnviarMensagemRequest("Olá");

        // Act
        var result = await controller.EnviarMensagem(9999, request);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task EnviarMensagem_NotAssignedAttendant_ReturnsForbidden()
    {
        // Arrange — atendimento is assigned to a different attendant (id=99), not the logged-in user (id=10)
        var atendimento = CreateAtendimento(AtendimentoStatusEnum.EmAndamento.ToString(), atendenteId: 99);
        var controller = CreateControllerWithUser(UserRoles.Atendente, _farmacia.Id); // userId defaults to "10"
        var request = new AtendimentosController.EnviarMensagemRequest("Tentativa indevida");

        // Act
        var result = await controller.EnviarMensagem(atendimento.Id, request);

        // Assert
        var statusResult = result as ObjectResult;
        statusResult.Should().NotBeNull();
        statusResult!.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task EnviarMensagem_GrupoVisualizacao_ReturnsBadRequestAndDoesNotPersistOutbound()
    {
        var atendimento = CreateGrupoVisualizacao(AtendimentoStatusEnum.EmAndamento.ToString(), atendenteId: 10);
        var controller = CreateControllerWithUser(UserRoles.Atendente, _farmacia.Id);
        var request = new AtendimentosController.EnviarMensagemRequest("Não deve enviar");

        var result = await controller.EnviarMensagem(atendimento.Id, request);

        result.Should().BeOfType<BadRequestObjectResult>();
        var mensagens = await _context.Mensagens
            .Where(m => m.AtendimentoId == atendimento.Id)
            .ToListAsync();
        mensagens.Should().BeEmpty();
    }

    [Fact]
    public async Task EnviarMensagem_WhatsAppSendFails_StillPersistsMensagem()
    {
        // Arrange
        _mockWhatsApp
            .Setup(s => s.SendTextMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("WhatsApp API unavailable"));

        // Atendimento must be assigned to the same user (id=10) that sends the message
        var atendimento = CreateAtendimento(AtendimentoStatusEnum.EmAndamento.ToString(), atendenteId: 10);
        var controller = CreateControllerWithUser(UserRoles.Atendente, _farmacia.Id);
        var request = new AtendimentosController.EnviarMensagemRequest("Mensagem mesmo com WA fora");

        // Act
        var result = await controller.EnviarMensagem(atendimento.Id, request);

        // Assert — message is persisted regardless of WhatsApp failure
        var ok = result as OkObjectResult;
        ok.Should().NotBeNull();

        var mensagens = await _context.Mensagens
            .Where(m => m.AtendimentoId == atendimento.Id)
            .ToListAsync();
        mensagens.Should().HaveCount(1);
        mensagens[0].Status.Should().Be(MessageStatus.Falha);
        mensagens[0].ErrorMessage.Should().Be("Não foi possível comunicar com o provedor WhatsApp.");

        var json = System.Text.Json.JsonSerializer.Serialize(ok!.Value);
        using var document = System.Text.Json.JsonDocument.Parse(json);
        document.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
    }

    // ─── Transferir ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Transferir_ValidRequest_ChangesAtendenteAndReturnsOk()
    {
        // Arrange
        var atendimento = CreateAtendimento(AtendimentoStatusEnum.EmAndamento.ToString());
        var targetAtendente = CreateAtendente("Atendente Destino", _farmacia.Id);
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var request = new AtendimentosController.TransferirAtendimentoRequest(targetAtendente.Id);

        // Act
        var result = await controller.Transferir(atendimento.Id, request);

        // Assert
        var ok = result as OkObjectResult;
        ok.Should().NotBeNull();
        ok!.StatusCode.Should().Be(200);

        var updated = await _context.Atendimentos.FindAsync(atendimento.Id);
        updated!.AtendenteId.Should().Be(targetAtendente.Id);
    }

    [Fact]
    public async Task Transferir_NonExistentAtendimento_ReturnsNotFound()
    {
        // Arrange
        var targetAtendente = CreateAtendente("Atendente Destino", _farmacia.Id);
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var request = new AtendimentosController.TransferirAtendimentoRequest(targetAtendente.Id);

        // Act
        var result = await controller.Transferir(9999, request);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Transferir_NonExistentTargetAtendente_ReturnsBadRequest()
    {
        // Arrange
        var atendimento = CreateAtendimento(AtendimentoStatusEnum.EmAndamento.ToString());
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var request = new AtendimentosController.TransferirAtendimentoRequest(9999);

        // Act
        var result = await controller.Transferir(atendimento.Id, request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Transferir_WrongFarmacia_ReturnsNotFound()
    {
        // Arrange
        var atendimento = CreateAtendimento(AtendimentoStatusEnum.EmAndamento.ToString());
        var controller = CreateControllerWithUser(UserRoles.Administrador, farmaciaId: 9999);
        var request = new AtendimentosController.TransferirAtendimentoRequest(1);

        // Act
        var result = await controller.Transferir(atendimento.Id, request);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Transferir_FinalizedAtendimento_ReturnsBadRequest()
    {
        // Arrange
        var atendimento = CreateAtendimento(AtendimentoStatusEnum.Finalizado.ToString());
        var targetAtendente = CreateAtendente("Atendente Destino", _farmacia.Id);
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var request = new AtendimentosController.TransferirAtendimentoRequest(targetAtendente.Id);

        // Act
        var result = await controller.Transferir(atendimento.Id, request);

        // Assert
        var bad = result as BadRequestObjectResult;
        bad.Should().NotBeNull();
        bad!.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Transferir_GrupoVisualizacao_ReturnsBadRequestAndDoesNotPromote()
    {
        var atendimento = CreateGrupoVisualizacao(AtendimentoStatusEnum.Pendente.ToString());
        var targetAtendente = CreateAtendente("Atendente Destino", _farmacia.Id);
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var request = new AtendimentosController.TransferirAtendimentoRequest(targetAtendente.Id);

        var result = await controller.Transferir(atendimento.Id, request);

        result.Should().BeOfType<BadRequestObjectResult>();
        var updated = await _context.Atendimentos.FindAsync(atendimento.Id);
        updated!.StatusEnum.Should().Be(AtendimentoStatusEnum.Pendente);
        updated.AtendenteId.Should().BeNull();
    }
}
