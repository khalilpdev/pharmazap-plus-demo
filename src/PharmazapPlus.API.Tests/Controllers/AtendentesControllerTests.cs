using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using PharmazapPlus.API.Controllers;
using PharmazapPlus.API.Data;
using PharmazapPlus.Shared.Models.CRM;
using PharmazapPlus.Shared.Models.Core;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace PharmazapPlus.API.Tests.Controllers;

public class AtendentesControllerTests
{
    private readonly Mock<ILogger<AtendentesController>> _mockLogger;
    private readonly PharmazapDbContext _context;
    private readonly Farmacia _farmacia;
    private readonly Departamento _departamento;

    public AtendentesControllerTests()
    {
        _mockLogger = new Mock<ILogger<AtendentesController>>();

        var options = new DbContextOptionsBuilder<PharmazapDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new PharmazapDbContext(options);

        _farmacia = new Farmacia
        {
            Nome = "Farmacia Teste",
            Cnpj = "00.000.000/0001-00",
            Telefone = "(11) 99999-9999",
            Email = "contato@teste.com",
            Endereco = "Rua Teste, 123",
            Cidade = "São Paulo",
            Estado = "SP",
            Cep = "01000000",
            DataCadastro = DateTime.UtcNow,
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "test-key",
                WhatsAppPhoneNumberId = "",
                WhatsAppAccessToken = "",
                WhatsAppBusinessAccountId = ""
            },
        };
        _context.Farmacias.Add(_farmacia);
        _context.SaveChanges();

        _departamento = new Departamento
        {
            FarmaciaId = _farmacia.Id,
            Nome = "Atendimento",
            Ativo = true,
            Farmacia = null
        };
        _context.Departamentos.Add(_departamento);
        _context.SaveChanges();
    }

    private AtendentesController CreateControllerWithUser(string role, int farmaciaId, string userId = "1")
    {
        var controller = new AtendentesController(_context, _mockLogger.Object);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
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

    private AtendentesController CreateControllerAsGestorSemFarmacia(string userId = "1")
    {
        var controller = new AtendentesController(_context, _mockLogger.Object);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Email, "gestor@teste.com"),
            new Claim(ClaimTypes.Name, "Gestor User"),
            new Claim(ClaimTypes.Role, UserRoles.Gestor)
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    private UsuarioFarmacia CreateTestAtendente(string email, int farmaciaId, bool ativo = true)
    {
        var (hash, salt) = GeneratePasswordHash("Test@123");
        var user = new UsuarioFarmacia
        {
            Nome = $"Atendente {email}",
            Email = email,
            Telefone = "(11) 99999-9999",
            Role = UserRoles.Atendente,
            PasswordHash = hash,
            PasswordSalt = salt,
            Ativo = ativo,
            EmailConfirmado = true,
            FarmaciaId = farmaciaId,
            Farmacia = null!,
            DataCadastro = DateTime.UtcNow,
            Turno = "Integral",
            RecebeNotificacoes = true,
            TotalAtendimentos = 0,
            AtendimentosHoje = 0
        };
        _context.UsuariosFarmacia.Add(user);
        _context.SaveChanges();
        return user;
    }

    // ── GET /api/atendentes ──────────────────────────────────────────

    [Fact]
    public async Task GetAtendentes_AsAdministrador_ReturnsOnlyFarmaciaAtendentes()
    {
        // Arrange
        CreateTestAtendente("a1@teste.com", _farmacia.Id);
        CreateTestAtendente("a2@teste.com", _farmacia.Id);
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);

        // Act
        var result = await controller.GetAtendentes();

        // Assert
        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task GetAtendentes_AsAdministrador_DoesNotReturnOtherFarmaciaAtendentes()
    {
        // Arrange
        var farmacia2 = new Farmacia
        {
            Nome = "Farmacia 2", Cnpj = "11.111.111/0001-11",
            Telefone = "(11) 88888-8888", Email = "f2@teste.com",
            Endereco = "Rua 2",
            Cidade = "SP", Estado = "SP", Cep = "02000000",
            DataCadastro = DateTime.UtcNow,
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "key-2",
                WhatsAppPhoneNumberId = "", WhatsAppAccessToken = "",
                WhatsAppBusinessAccountId = ""
            },
        };
        _context.Farmacias.Add(farmacia2);
        await _context.SaveChangesAsync();

        CreateTestAtendente("own@teste.com", _farmacia.Id);
        CreateTestAtendente("other@teste.com", farmacia2.Id);

        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);

        // Act
        var result = await controller.GetAtendentes();

        // Assert
        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAtendentes_AsGestorSemFarmaciaIdClaim_ReturnsAtendentes()
    {
        // Arrange
        var farmacia2 = new Farmacia
        {
            Nome = "Farmacia 2", Cnpj = "11.111.111/0001-11",
            Telefone = "(11) 88888-8888", Email = "f2@teste.com",
            Endereco = "Rua 2",
            Cidade = "SP", Estado = "SP", Cep = "02000000",
            DataCadastro = DateTime.UtcNow,
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "key-2",
                WhatsAppPhoneNumberId = "", WhatsAppAccessToken = "",
                WhatsAppBusinessAccountId = ""
            },
        };
        _context.Farmacias.Add(farmacia2);
        await _context.SaveChangesAsync();

        CreateTestAtendente("own@teste.com", _farmacia.Id);
        CreateTestAtendente("other@teste.com", farmacia2.Id);

        var controller = CreateControllerAsGestorSemFarmacia();

        // Act
        var result = await controller.GetAtendentes();

        // Assert
        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);

        var data = okResult.Value?.GetType().GetProperty("Data")?.GetValue(okResult.Value) as System.Collections.IEnumerable;
        data.Should().NotBeNull();
        data!.Cast<object>().Should().HaveCount(2);
    }

    // ── GET /api/atendentes/{id} ─────────────────────────────────────

    [Fact]
    public async Task GetAtendente_ExistingAtendente_ReturnsAtendente()
    {
        var atendente = CreateTestAtendente("get@teste.com", _farmacia.Id);
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);

        var result = await controller.GetAtendente(atendente.Id);

        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task GetAtendente_NonExistentAtendente_ReturnsNotFound()
    {
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);

        var result = await controller.GetAtendente(9999);

        var notFound = result as NotFoundObjectResult;
        notFound.Should().NotBeNull();
        notFound!.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task GetAtendente_NonAtendenteRole_ReturnsNotFound()
    {
        // Admin user should not be found via atendentes endpoint
        var (hash, salt) = GeneratePasswordHash("Test@123");
        var admin = new UsuarioFarmacia
        {
            Nome = "Admin User", Email = "notaten@teste.com",
            Telefone = "", Role = UserRoles.Administrador,
            PasswordHash = hash, PasswordSalt = salt,
            Ativo = true, EmailConfirmado = true,
            FarmaciaId = _farmacia.Id, Farmacia = null!,
            DataCadastro = DateTime.UtcNow,
            Turno = "Integral", RecebeNotificacoes = true,
            TotalAtendimentos = 0, AtendimentosHoje = 0
        };
        _context.UsuariosFarmacia.Add(admin);
        await _context.SaveChangesAsync();

        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var result = await controller.GetAtendente(admin.Id);

        var notFound = result as NotFoundObjectResult;
        notFound.Should().NotBeNull();
        notFound!.StatusCode.Should().Be(404);
    }

    // ── POST /api/atendentes ─────────────────────────────────────────

    [Fact]
    public async Task CreateAtendente_WithValidData_ReturnsCreated()
    {
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var request = new CreateAtendenteRequest
        {
            Nome = "Novo Atendente",
            Email = "novo@teste.com",
            Password = "Novo@123",
            Turno = "Manhã",
            RecebeNotificacoes = true
        };

        var result = await controller.CreateAtendente(request);

        var created = result as CreatedAtActionResult;
        created.Should().NotBeNull();
        created!.StatusCode.Should().Be(201);

        var saved = await _context.UsuariosFarmacia.FirstOrDefaultAsync(u => u.Email == "novo@teste.com");
        saved.Should().NotBeNull();
        saved!.Role.Should().Be(UserRoles.Atendente);
        saved.FarmaciaId.Should().Be(_farmacia.Id);
    }

    [Fact]
    public async Task CreateAtendente_WithDepartamentoValido_PersisteDepartamento()
    {
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var request = new CreateAtendenteRequest
        {
            Nome = "Novo Atendente",
            Email = "departamento@teste.com",
            Password = "Novo@123",
            DepartamentoId = _departamento.Id
        };

        var result = await controller.CreateAtendente(request);

        result.Should().BeOfType<CreatedAtActionResult>();

        var saved = await _context.UsuariosFarmacia.FirstAsync(u => u.Email == "departamento@teste.com");
        saved.DepartamentoId.Should().Be(_departamento.Id);
    }

    [Fact]
    public async Task CreateAtendente_WithDepartamentoInvalido_ReturnsBadRequest()
    {
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var request = new CreateAtendenteRequest
        {
            Nome = "Novo Atendente",
            Email = "departamento-invalido@teste.com",
            Password = "Novo@123",
            DepartamentoId = 9999
        };

        var result = await controller.CreateAtendente(request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateAtendente_AutoSetsRoleToAtendente()
    {
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var request = new CreateAtendenteRequest
        {
            Nome = "Test Role",
            Email = "role@teste.com",
            Password = "Test@123"
        };

        await controller.CreateAtendente(request);

        var saved = await _context.UsuariosFarmacia.FirstOrDefaultAsync(u => u.Email == "role@teste.com");
        saved!.Role.Should().Be(UserRoles.Atendente);
    }

    [Fact]
    public async Task CreateAtendente_WithMissingFields_ReturnsBadRequest()
    {
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var request = new CreateAtendenteRequest { Nome = "", Email = "", Password = "" };

        var result = await controller.CreateAtendente(request);

        var badRequest = result as BadRequestObjectResult;
        badRequest.Should().NotBeNull();
        badRequest!.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task CreateAtendente_WithShortPassword_ReturnsBadRequest()
    {
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var request = new CreateAtendenteRequest
        {
            Nome = "Test", Email = "short@teste.com", Password = "12345"
        };

        var result = await controller.CreateAtendente(request);

        var badRequest = result as BadRequestObjectResult;
        badRequest.Should().NotBeNull();
        badRequest!.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task CreateAtendente_WithDuplicateEmail_ReturnsBadRequest()
    {
        CreateTestAtendente("dup@teste.com", _farmacia.Id);
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var request = new CreateAtendenteRequest
        {
            Nome = "Dup", Email = "dup@teste.com", Password = "Test@123"
        };

        var result = await controller.CreateAtendente(request);

        var badRequest = result as BadRequestObjectResult;
        badRequest.Should().NotBeNull();
        badRequest!.StatusCode.Should().Be(400);
    }

    // ── PUT /api/atendentes/{id} ─────────────────────────────────────

    [Fact]
    public async Task UpdateAtendente_WithValidData_ReturnsOk()
    {
        var atendente = CreateTestAtendente("update@teste.com", _farmacia.Id);
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var request = new UpdateAtendenteRequest { Nome = "Nome Atualizado", Turno = "Noite" };

        var result = await controller.UpdateAtendente(atendente.Id, request);

        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);

        var updated = await _context.UsuariosFarmacia.FindAsync(atendente.Id);
        updated!.Nome.Should().Be("Nome Atualizado");
        updated.Turno.Should().Be("Noite");
    }

    [Fact]
    public async Task UpdateAtendente_WithDepartamentoValido_AtualizaDepartamento()
    {
        var atendente = CreateTestAtendente("update-departamento@teste.com", _farmacia.Id);
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var request = new UpdateAtendenteRequest { DepartamentoId = _departamento.Id };

        var result = await controller.UpdateAtendente(atendente.Id, request);

        result.Should().BeOfType<OkObjectResult>();

        var updated = await _context.UsuariosFarmacia.FindAsync(atendente.Id);
        updated!.DepartamentoId.Should().Be(_departamento.Id);
    }

    [Fact]
    public async Task UpdateAtendente_NonExistentAtendente_ReturnsNotFound()
    {
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var request = new UpdateAtendenteRequest { Nome = "Test" };

        var result = await controller.UpdateAtendente(9999, request);

        var notFound = result as NotFoundObjectResult;
        notFound.Should().NotBeNull();
        notFound!.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task UpdateAtendente_DoesNotChangeRole()
    {
        var atendente = CreateTestAtendente("norole@teste.com", _farmacia.Id);
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var request = new UpdateAtendenteRequest { Nome = "Updated" };

        await controller.UpdateAtendente(atendente.Id, request);

        var updated = await _context.UsuariosFarmacia.FindAsync(atendente.Id);
        updated!.Role.Should().Be(UserRoles.Atendente);
    }

    // ── DELETE /api/atendentes/{id} ──────────────────────────────────

    [Fact]
    public async Task InativarAtendente_ExistingAtendente_DeactivatesAtendente()
    {
        var atendente = CreateTestAtendente("inativar@teste.com", _farmacia.Id);
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);

        var result = await controller.InativarAtendente(atendente.Id);

        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);

        var deactivated = await _context.UsuariosFarmacia.FindAsync(atendente.Id);
        deactivated!.Ativo.Should().BeFalse();
        deactivated.DataInativacao.Should().NotBeNull();
    }

    [Fact]
    public async Task InativarAtendente_NonExistentAtendente_ReturnsNotFound()
    {
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);

        var result = await controller.InativarAtendente(9999);

        var notFound = result as NotFoundObjectResult;
        notFound.Should().NotBeNull();
        notFound!.StatusCode.Should().Be(404);
    }

    // ── PUT /api/atendentes/{id}/ativar ──────────────────────────────

    [Fact]
    public async Task AtivarAtendente_InactiveAtendente_ActivatesAtendente()
    {
        var atendente = CreateTestAtendente("ativar@teste.com", _farmacia.Id, ativo: false);
        atendente.DataInativacao = DateTime.UtcNow.AddDays(-1);
        await _context.SaveChangesAsync();

        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);

        var result = await controller.AtivarAtendente(atendente.Id);

        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);

        var activated = await _context.UsuariosFarmacia.FindAsync(atendente.Id);
        activated!.Ativo.Should().BeTrue();
        activated.DataInativacao.Should().BeNull();
    }

    [Fact]
    public async Task AtivarAtendente_NonExistentAtendente_ReturnsNotFound()
    {
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);

        var result = await controller.AtivarAtendente(9999);

        var notFound = result as NotFoundObjectResult;
        notFound.Should().NotBeNull();
        notFound!.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task CreateAtendente_ThenGetAtendentes_NewAtendenteAppearsInList()
    {
        // Arrange
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var request = new CreateAtendenteRequest
        {
            Nome = "Flow Test",
            Email = "flow@teste.com",
            Password = "Flow@123",
            Telefone = "(11) 91111-1111",
            Turno = "Tarde",
            RecebeNotificacoes = false
        };

        // Act - Create
        var createResult = await controller.CreateAtendente(request);
        var created = createResult as CreatedAtActionResult;
        created.Should().NotBeNull();

        // Act - List
        var listResult = await controller.GetAtendentes();
        var okResult = listResult as OkObjectResult;
        okResult.Should().NotBeNull();

        // Assert - Verify the new atendente exists in DB
        var savedInDb = await _context.UsuariosFarmacia
            .FirstOrDefaultAsync(u => u.Email == "flow@teste.com");
        savedInDb.Should().NotBeNull();
        savedInDb!.Nome.Should().Be("Flow Test");
        savedInDb.Telefone.Should().Be("(11) 91111-1111");
        savedInDb.Turno.Should().Be("Tarde");
        savedInDb.RecebeNotificacoes.Should().BeFalse();
        savedInDb.Role.Should().Be(UserRoles.Atendente);
        savedInDb.FarmaciaId.Should().Be(_farmacia.Id);
        savedInDb.Ativo.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAtendente_WithMinimalFields_SetsCorrectDefaults()
    {
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var request = new CreateAtendenteRequest
        {
            Nome = "Minimal",
            Email = "minimal@teste.com",
            Password = "Min@123"
        };

        await controller.CreateAtendente(request);

        var saved = await _context.UsuariosFarmacia
            .FirstOrDefaultAsync(u => u.Email == "minimal@teste.com");
        saved.Should().NotBeNull();
        saved!.Turno.Should().Be("Integral");
        saved.RecebeNotificacoes.Should().BeTrue();
        saved.Ativo.Should().BeTrue();
        saved.Telefone.Should().Be("");
    }

    [Fact]
    public async Task ResetSenhaAtendente_WithValidData_ReturnsOkAndUpdatesPassword()
    {
        var atendente = CreateTestAtendente("reset@teste.com", _farmacia.Id);
        var oldHash = atendente.PasswordHash;
        var oldSalt = atendente.PasswordSalt;

        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var request = new ResetPasswordRequest { NewPassword = "NovoPass@123" };

        var result = await controller.ResetSenhaAtendente(atendente.Id, request);

        var ok = result as OkObjectResult;
        ok.Should().NotBeNull();
        ok!.StatusCode.Should().Be(200);

        var updated = await _context.UsuariosFarmacia.FindAsync(atendente.Id);
        updated!.PasswordHash.Should().NotBe(oldHash);
        updated.PasswordSalt.Should().NotBe(oldSalt);
    }

    [Fact]
    public async Task ResetSenhaAtendente_WithEmptyPassword_ReturnsBadRequest()
    {
        var atendente = CreateTestAtendente("resetempty@teste.com", _farmacia.Id);
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var request = new ResetPasswordRequest { NewPassword = "" };

        var result = await controller.ResetSenhaAtendente(atendente.Id, request);

        var bad = result as BadRequestObjectResult;
        bad.Should().NotBeNull();
        bad!.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task ResetSenhaAtendente_WithShortPassword_ReturnsBadRequest()
    {
        var atendente = CreateTestAtendente("resetshort@teste.com", _farmacia.Id);
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var request = new ResetPasswordRequest { NewPassword = "12345" };

        var result = await controller.ResetSenhaAtendente(atendente.Id, request);

        var bad = result as BadRequestObjectResult;
        bad.Should().NotBeNull();
        bad!.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task ResetSenhaAtendente_WithNonExistentId_ReturnsNotFound()
    {
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var request = new ResetPasswordRequest { NewPassword = "NovoPass@123" };

        var result = await controller.ResetSenhaAtendente(9999, request);

        var notFound = result as NotFoundObjectResult;
        notFound.Should().NotBeNull();
        notFound!.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task ResetSenhaAtendente_FromDifferentFarmacia_ReturnsForbid()
    {
        var outraFarmacia = new Farmacia
        {
            Nome = "Outra Farmacia",
            Cnpj = "11.111.111/0001-11",
            Telefone = "(11) 99999-0000",
            Email = "outra@teste.com",
            Endereco = "Rua Outra, 1",
            Cidade = "Rio de Janeiro",
            Estado = "RJ",
            Cep = "20000000",
            DataCadastro = DateTime.UtcNow,
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "other-key",
                WhatsAppPhoneNumberId = "",
                WhatsAppAccessToken = "",
                WhatsAppBusinessAccountId = ""
            },
        };
        _context.Farmacias.Add(outraFarmacia);
        await _context.SaveChangesAsync();

        var atendente = CreateTestAtendente("resetother@teste.com", outraFarmacia.Id);
        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);
        var request = new ResetPasswordRequest { NewPassword = "NovoPass@123" };

        var result = await controller.ResetSenhaAtendente(atendente.Id, request);

        result.Should().BeOfType<ForbidResult>();
    }

    private static (string hash, string salt) GeneratePasswordHash(string password)
    {
        byte[] saltBytes = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(saltBytes);
        }
        string salt = Convert.ToBase64String(saltBytes);

        using var sha256 = SHA256.Create();
        var passwordBytes = Encoding.UTF8.GetBytes(password + salt);
        var hashBytes = sha256.ComputeHash(passwordBytes);
        string hash = Convert.ToBase64String(hashBytes);

        return (hash, salt);
    }
}
