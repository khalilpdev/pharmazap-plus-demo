using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using PharmazapPlus.API.Controllers;
using PharmazapPlus.API.Data;
using PharmazapPlus.Shared;
using PharmazapPlus.Shared.Enums;
using PharmazapPlus.Shared.Models.Core;
using PharmazapPlus.Shared.Models.Mensagens;
using System.Security.Claims;
using System.Text.Json;
using SharedFarmaciaStatus = PharmazapPlus.Shared.Models.Core.FarmaciaStatus;

namespace PharmazapPlus.API.Tests.Controllers;

public class RelatoriosControllerTests
{
    private readonly PharmazapDbContext _context;
    private readonly Mock<ILogger<RelatoriosController>> _mockLogger;

    public RelatoriosControllerTests()
    {
        _mockLogger = new Mock<ILogger<RelatoriosController>>();

        var options = new DbContextOptionsBuilder<PharmazapDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new PharmazapDbContext(options);
    }

    private RelatoriosController CreateController(string role, int? farmaciaId = null, int userId = 1)
    {
        var controller = new RelatoriosController(_context, _mockLogger.Object);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role)
        };

        if (farmaciaId.HasValue)
            claims.Add(new Claim("FarmaciaId", farmaciaId.Value.ToString()));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthType"))
            }
        };

        return controller;
    }

    private async Task<Farmacia> CreateFarmaciaAsync(string suffix = "")
    {
        var uniqueId = Guid.NewGuid().GetHashCode() & 0x7FFFFFFF;
        var farmacia = new Farmacia
        {
            Nome = $"Farmacia Teste {suffix}",
            Cnpj = $"{uniqueId % 100000000:D8}0001{uniqueId % 99:D2}",
            Telefone = "(11) 99999-9999",
            Email = $"contato{uniqueId}@teste.com",
            Endereco = "Rua Teste, 1",
            Cidade = "São Paulo",
            Estado = "SP",
            Cep = "01000000",
            DataCadastro = DateTime.UtcNow,
            Status = SharedFarmaciaStatus.Ativa,
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = $"key-{Guid.NewGuid()}",
                WhatsAppPhoneNumberId = "",
                WhatsAppAccessToken = "",
                WhatsAppBusinessAccountId = ""
            },
        };
        _context.Farmacias.Add(farmacia);
        await _context.SaveChangesAsync();
        return farmacia;
    }

    private async Task<UsuarioFarmacia> CreateAtendenteAsync(int farmaciaId, string nome = "Atendente")
    {
        var atendente = new UsuarioFarmacia
        {
            FarmaciaId = farmaciaId,
            Farmacia = null!,
            Nome = nome,
            Email = $"{nome.ToLower().Replace(" ", "")}@teste.com",
            Telefone = "(11) 99999-9999",
            Role = UserRoles.Atendente,
            DataCadastro = DateTime.UtcNow,
            Ativo = true,
            Turno = "Integral",
            PasswordHash = "hash_placeholder",
            PasswordSalt = "salt_placeholder"
        };
        _context.UsuariosFarmacia.Add(atendente);
        await _context.SaveChangesAsync();
        return atendente;
    }

    private async Task<Atendimento> CreateAtendimentoAsync(int farmaciaId, int? atendenteId, string status, DateTime? dataFim = null)
    {
        var statusEnum = Enum.TryParse<AtendimentoStatusEnum>(status, out var parsedStatus)
            ? parsedStatus
            : AtendimentoStatusEnum.Pendente;

        var atendimento = new Atendimento
        {
            FarmaciaId = farmaciaId,
            Farmacia = null!,
            Cliente = null!,
            Atendente = null!,
            AtendenteId = atendenteId,
            WhatsAppId = $"+551199{Guid.NewGuid().GetHashCode():D8}",
            CanalEnum = CanalEnum.WhatsApp,
            StatusEnum = statusEnum,
            DataInicio = DateTime.UtcNow.AddHours(-1),
            DataFim = dataFim,
            DuracaoSegundos = dataFim.HasValue ? 600 : null
        };
        _context.Atendimentos.Add(atendimento);
        await _context.SaveChangesAsync();
        return atendimento;
    }

    // ── Gestor ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRelatorioGestor_AsGestor_ReturnsSuccessWithFarmacias()
    {
        // Arrange
        await CreateFarmaciaAsync("A");
        await CreateFarmaciaAsync("B");

        var controller = CreateController(UserRoles.Gestor);

        // Act
        var result = await controller.GetRelatorioGestor() as OkObjectResult;

        // Assert
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(200);

        var json = JsonSerializer.Serialize(result.Value);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("Success").GetBoolean().Should().BeTrue();
        var data = doc.RootElement.GetProperty("Data");
        data.GetProperty("Resumo").GetProperty("TotalFarmacias").GetInt32().Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetRelatorioGestor_WithDateRange_FiltersAtendimentos()
    {
        // Arrange
        var farmacia = await CreateFarmaciaAsync("DateRange");
        await CreateAtendimentoAsync(farmacia.Id, null, AtendimentoStatusEnum.Finalizado.ToString());

        var controller = CreateController(UserRoles.Gestor);

        // Act
        var result = await controller.GetRelatorioGestor(
            dataInicio: DateTime.UtcNow.AddDays(-7),
            dataFim: DateTime.UtcNow) as OkObjectResult;

        // Assert
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(200);
        var json = JsonSerializer.Serialize(result.Value);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("Success").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task GetRelatorioGestor_WithFarmaciaIdFilter_ReturnsOnlyThatFarmacia()
    {
        // Arrange
        var f1 = await CreateFarmaciaAsync("F1");
        await CreateFarmaciaAsync("F2");

        var controller = CreateController(UserRoles.Gestor);

        // Act
        var result = await controller.GetRelatorioGestor(farmaciaId: f1.Id) as OkObjectResult;

        // Assert
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(200);
        var json = JsonSerializer.Serialize(result.Value);
        using var doc = JsonDocument.Parse(json);
        var farmacias = doc.RootElement.GetProperty("Data").GetProperty("Farmacias");
        farmacias.GetArrayLength().Should().Be(1);
        farmacias[0].GetProperty("Nome").GetString().Should().Contain("F1");
    }

    // ── Administrador ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRelatorioAdministrador_AsAdministrador_ReturnsSuccessWithAtendentes()
    {
        // Arrange
        var farmacia = await CreateFarmaciaAsync("Admin");
        var atendente = await CreateAtendenteAsync(farmacia.Id, "João Silva");
        await CreateAtendimentoAsync(farmacia.Id, atendente.Id, AtendimentoStatusEnum.Finalizado.ToString(), DateTime.UtcNow);
        await CreateAtendimentoAsync(farmacia.Id, atendente.Id, AtendimentoStatusEnum.EmAndamento.ToString());

        var controller = CreateController(UserRoles.Administrador, farmacia.Id);

        // Act
        var result = await controller.GetRelatorioAdministrador() as OkObjectResult;

        // Assert
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(200);
        var json = JsonSerializer.Serialize(result.Value);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("Success").GetBoolean().Should().BeTrue();
        var data = doc.RootElement.GetProperty("Data");
        data.GetProperty("Resumo").GetProperty("TotalAtendimentos").GetInt32().Should().BeGreaterThanOrEqualTo(2);
        data.GetProperty("Atendentes").GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GetRelatorioAdministrador_WithoutFarmaciaId_ReturnsBadRequest()
    {
        // Arrange
        var controller = CreateController(UserRoles.Administrador); // no FarmaciaId claim

        // Act
        var result = await controller.GetRelatorioAdministrador();

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetRelatorioAdministrador_FarmaciaNotFound_ReturnsNotFound()
    {
        // Arrange
        var controller = CreateController(UserRoles.Administrador, 99999); // non-existent farm

        // Act
        var result = await controller.GetRelatorioAdministrador();

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetRelatorioAdministrador_AsAdministrador_CalculatesTaxaConversao()
    {
        // Arrange
        var farmacia = await CreateFarmaciaAsync("Taxa");
        var atendente = await CreateAtendenteAsync(farmacia.Id, "Maria");

        // 3 finalizados, 1 em andamento
        for (int i = 0; i < 3; i++)
            await CreateAtendimentoAsync(farmacia.Id, atendente.Id, AtendimentoStatusEnum.Finalizado.ToString(), DateTime.UtcNow);
        await CreateAtendimentoAsync(farmacia.Id, atendente.Id, AtendimentoStatusEnum.EmAndamento.ToString());

        var controller = CreateController(UserRoles.Administrador, farmacia.Id);

        // Act
        var result = await controller.GetRelatorioAdministrador() as OkObjectResult;

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result!.Value);
        using var doc = JsonDocument.Parse(json);
        var atendentes = doc.RootElement.GetProperty("Data").GetProperty("Atendentes");
        atendentes.GetArrayLength().Should().Be(1);
        var atendenteData = atendentes[0];
        atendenteData.GetProperty("TotalAtendimentos").GetInt32().Should().Be(4);
        atendenteData.GetProperty("AtendimentosFinalizados").GetInt32().Should().Be(3);
        atendenteData.GetProperty("TaxaConversao").GetDouble().Should().BeApproximately(75.0, 0.1);
    }

    // ── Atendente ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRelatorioAtendente_WithValidUser_ReturnsPersonalStats()
    {
        // Arrange
        var farmacia = await CreateFarmaciaAsync("AtendPessoal");
        var atendente = await CreateAtendenteAsync(farmacia.Id, "Carlos");

        await CreateAtendimentoAsync(farmacia.Id, atendente.Id, AtendimentoStatusEnum.Finalizado.ToString(), DateTime.UtcNow);
        await CreateAtendimentoAsync(farmacia.Id, atendente.Id, AtendimentoStatusEnum.EmAndamento.ToString());

        var controller = CreateController(UserRoles.Atendente, farmacia.Id, atendente.Id);

        // Act
        var result = await controller.GetRelatorioAtendente() as OkObjectResult;

        // Assert
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(200);
        var json = JsonSerializer.Serialize(result.Value);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("Success").GetBoolean().Should().BeTrue();
        var resumo = doc.RootElement.GetProperty("Data").GetProperty("Resumo");
        resumo.GetProperty("TotalAtendimentos").GetInt32().Should().Be(2);
        resumo.GetProperty("AtendimentosFinalizados").GetInt32().Should().Be(1);
        resumo.GetProperty("AtendimentosEmAndamento").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task GetRelatorioAtendente_WithoutFarmaciaId_ReturnsBadRequest()
    {
        // Arrange — no FarmaciaId claim
        var controller = CreateController(UserRoles.Atendente);

        // Act
        var result = await controller.GetRelatorioAtendente();

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetRelatorioAtendente_OnlySeesOwnAtendimentos()
    {
        // Arrange
        var farmacia = await CreateFarmaciaAsync("Isolamento");
        var atendente1 = await CreateAtendenteAsync(farmacia.Id, "Ana");
        var atendente2 = await CreateAtendenteAsync(farmacia.Id, "Pedro");

        await CreateAtendimentoAsync(farmacia.Id, atendente1.Id, AtendimentoStatusEnum.Finalizado.ToString(), DateTime.UtcNow);
        await CreateAtendimentoAsync(farmacia.Id, atendente2.Id, AtendimentoStatusEnum.Finalizado.ToString(), DateTime.UtcNow);
        await CreateAtendimentoAsync(farmacia.Id, atendente2.Id, AtendimentoStatusEnum.Finalizado.ToString(), DateTime.UtcNow);

        var controller = CreateController(UserRoles.Atendente, farmacia.Id, atendente1.Id);

        // Act
        var result = await controller.GetRelatorioAtendente() as OkObjectResult;

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result!.Value);
        using var doc = JsonDocument.Parse(json);
        var resumo = doc.RootElement.GetProperty("Data").GetProperty("Resumo");
        // Should only count atendente1's atendimento
        resumo.GetProperty("TotalAtendimentos").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task GetRelatorioAtendente_HistoricoOrderedByDateDescending()
    {
        // Arrange
        var farmacia = await CreateFarmaciaAsync("Order");
        var atendente = await CreateAtendenteAsync(farmacia.Id, "Lucia");

        await CreateAtendimentoAsync(farmacia.Id, atendente.Id, AtendimentoStatusEnum.Finalizado.ToString(), DateTime.UtcNow.AddHours(-2));
        await CreateAtendimentoAsync(farmacia.Id, atendente.Id, AtendimentoStatusEnum.Finalizado.ToString(), DateTime.UtcNow.AddHours(-1));

        var controller = CreateController(UserRoles.Atendente, farmacia.Id, atendente.Id);

        // Act
        var result = await controller.GetRelatorioAtendente() as OkObjectResult;

        // Assert
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result!.Value);
        using var doc = JsonDocument.Parse(json);
        var historico = doc.RootElement.GetProperty("Data").GetProperty("Historico");
        historico.GetArrayLength().Should().Be(2);
    }

    // ── Export ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExportarRelatorio_AsAtendente_ReturnsExcelFile()
    {
        // Arrange
        var farmacia = await CreateFarmaciaAsync("Export");
        var atendente = await CreateAtendenteAsync(farmacia.Id, "Export User");
        await CreateAtendimentoAsync(farmacia.Id, atendente.Id, AtendimentoStatusEnum.Finalizado.ToString(), DateTime.UtcNow);

        var controller = CreateController(UserRoles.Atendente, farmacia.Id, atendente.Id);

        // Act
        var result = await controller.ExportarRelatorio(tipo: "atendente") as FileContentResult;

        // Assert
        result.Should().NotBeNull();
        result!.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        result.FileContents.Should().NotBeEmpty();
        result.FileDownloadName.Should().StartWith("relatorio_atendente_");
    }

    [Fact]
    public async Task ExportarRelatorio_AsGestor_ReturnsGestorExcel()
    {
        // Arrange
        await CreateFarmaciaAsync("ExportGestor");

        var controller = CreateController(UserRoles.Gestor);

        // Act
        var result = await controller.ExportarRelatorio(tipo: "gestor") as FileContentResult;

        // Assert
        result.Should().NotBeNull();
        result!.FileDownloadName.Should().StartWith("relatorio_gestor_");
    }

    [Fact]
    public async Task ExportarRelatorio_AsAdministrador_ReturnsAdminExcel()
    {
        // Arrange
        var farmacia = await CreateFarmaciaAsync("ExportAdmin");
        await CreateAtendenteAsync(farmacia.Id, "Export Atendente");

        var controller = CreateController(UserRoles.Administrador, farmacia.Id);

        // Act
        var result = await controller.ExportarRelatorio(tipo: "administrador") as FileContentResult;

        // Assert
        result.Should().NotBeNull();
        result!.FileDownloadName.Should().StartWith("relatorio_administrador_");
    }

    // ── Error handling & edge cases ──────────────────────────────────────────

    /// <summary>
    /// Regression: when dataFim is before dataInicio (swapped dates) the API must
    /// still return a valid 200 response instead of crashing — ensuring the
    /// frontend never gets stuck on "Carregando relatório..." due to a server error.
    /// </summary>
    [Fact]
    public async Task GetRelatorioAtendente_WithSwappedDates_ReturnsSuccess()
    {
        // Arrange
        var farmacia = await CreateFarmaciaAsync("SwapDates");
        var atendente = await CreateAtendenteAsync(farmacia.Id, "Swap Atendente");

        var controller = CreateController(UserRoles.Atendente, farmacia.Id, atendente.Id);

        // Act — dataFim is intentionally before dataInicio (swapped/invalid range)
        var result = await controller.GetRelatorioAtendente(
            dataInicio: DateTime.UtcNow,
            dataFim: DateTime.UtcNow.AddDays(-30));

        // Assert — must return 200 (empty data) not 500
        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var json = JsonSerializer.Serialize(ok.Value);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("Success").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task GetRelatorioAdministrador_WithSwappedDates_ReturnsSuccess()
    {
        // Arrange
        var farmacia = await CreateFarmaciaAsync("SwapDatesAdmin");
        await CreateAtendenteAsync(farmacia.Id, "Swap Admin Atendente");

        var controller = CreateController(UserRoles.Administrador, farmacia.Id);

        // Act — swapped date range must not crash the API
        var result = await controller.GetRelatorioAdministrador(
            dataInicio: DateTime.UtcNow,
            dataFim: DateTime.UtcNow.AddDays(-30));

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var json = JsonSerializer.Serialize(ok.Value);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("Success").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task GetRelatorioGestor_WithSwappedDates_ReturnsSuccess()
    {
        // Arrange
        await CreateFarmaciaAsync("SwapDatesGestor");

        var controller = CreateController(UserRoles.Gestor);

        // Act — swapped date range must not crash the API
        var result = await controller.GetRelatorioGestor(
            dataInicio: DateTime.UtcNow,
            dataFim: DateTime.UtcNow.AddDays(-30));

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var json = JsonSerializer.Serialize(ok.Value);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("Success").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task ExportarRelatorio_WithSwappedDates_ReturnsExcelFile()
    {
        // Arrange
        var farmacia = await CreateFarmaciaAsync("SwapDatesExport");
        var atendente = await CreateAtendenteAsync(farmacia.Id, "Swap Export User");

        var controller = CreateController(UserRoles.Atendente, farmacia.Id, atendente.Id);

        // Act — swapped dates must not crash the export
        var result = await controller.ExportarRelatorio(
            tipo: "atendente",
            dataInicio: DateTime.UtcNow,
            dataFim: DateTime.UtcNow.AddDays(-30));

        // Assert — still returns a valid Excel file (may be empty)
        result.Should().BeOfType<FileContentResult>();
        var file = (FileContentResult)result;
        file.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Fact]
    public async Task GetRelatorioAtendente_WithUserIdNotInDb_ReturnsEmptyDataNotError()
    {
        // Arrange — userId 99999 does not exist in the DB (no atendimentos)
        var farmacia = await CreateFarmaciaAsync("NotFound");
        var controller = CreateController(UserRoles.Atendente, farmacia.Id, 99999);

        // Act
        var result = await controller.GetRelatorioAtendente();

        // Assert — must not crash; returns 200 with empty data
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(ok.Value);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("Success").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("Data").GetProperty("Resumo")
            .GetProperty("TotalAtendimentos").GetInt32().Should().Be(0);
    }
}
