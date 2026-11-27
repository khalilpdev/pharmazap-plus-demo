using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using PharmazapPlus.API.Controllers;
using PharmazapPlus.API.Data;
using PharmazapPlus.Shared.Models.Core;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace PharmazapPlus.API.Tests.Controllers;

public class AdministradoresControllerTests
{
    private readonly Mock<ILogger<AdministradoresController>> _mockLogger;
    private readonly PharmazapDbContext _context;
    private readonly Farmacia _farmacia;

    public AdministradoresControllerTests()
    {
        _mockLogger = new Mock<ILogger<AdministradoresController>>();

        var options = new DbContextOptionsBuilder<PharmazapDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new PharmazapDbContext(options);

        // Create test pharmacy
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
    }

    private AdministradoresController CreateControllerWithUser(string role, int farmaciaId, string userId = "1")
    {
        var controller = new AdministradoresController(_context, _mockLogger.Object);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Email, "user@teste.com"),
            new Claim(ClaimTypes.Name, "Test User"),
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

    private UsuarioFarmacia CreateTestUser(string email, string role, int farmaciaId)
    {
        var (hash, salt) = GeneratePasswordHash("Test@123");
        var user = new UsuarioFarmacia
        {
            Nome = $"User {email}",
            Email = email,
            Telefone = "(11) 99999-9999",
            Role = role,
            PasswordHash = hash,
            PasswordSalt = salt,
            Ativo = true,
            EmailConfirmado = true,
            FarmaciaId = farmaciaId,
            DataCadastro = DateTime.UtcNow,
            Turno = "Integral",
            RecebeNotificacoes = true,
            TotalAtendimentos = 0,
            AtendimentosHoje = 0,
            Farmacia = null!
        };
        _context.UsuariosFarmacia.Add(user);
        _context.SaveChanges();
        return user;
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        var controller = new AdministradoresController(_context, _mockLogger.Object);
        controller.Should().NotBeNull();
    }

    // ── GET /api/administradores ────────────────────────────────────────────

    [Fact]
    public async Task GetUsuarios_AsGestor_ReturnsAllUsers()
    {
        // Arrange
        CreateTestUser("user1@teste.com", UserRoles.Administrador, _farmacia.Id);
        CreateTestUser("user2@teste.com", UserRoles.Atendente, _farmacia.Id);
        var controller = CreateControllerWithUser(UserRoles.Gestor, _farmacia.Id);

        // Act
        var result = await controller.GetAdministradores();

        // Assert
        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task GetUsuarios_AsAdministrador_ReturnsOnlyFarmaciaUsers()
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

        CreateTestUser("user1@teste.com", UserRoles.Atendente, _farmacia.Id);
        CreateTestUser("user2@teste.com", UserRoles.Atendente, farmacia2.Id);

        var controller = CreateControllerWithUser(UserRoles.Administrador, _farmacia.Id);

        // Act
        var result = await controller.GetAdministradores();

        // Assert
        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
    }

    // ── GET /api/administradores/{id} ───────────────────────────────────────

    [Fact]
    public async Task GetUsuario_ExistingUser_ReturnsUser()
    {
        // Arrange
        var user = CreateTestUser("target@teste.com", UserRoles.Atendente, _farmacia.Id);
        var controller = CreateControllerWithUser(UserRoles.Gestor, _farmacia.Id);

        // Act
        var result = await controller.GetAdministrador(user.Id);

        // Assert
        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task GetUsuario_NonExistentUser_ReturnsNotFound()
    {
        var controller = CreateControllerWithUser(UserRoles.Gestor, _farmacia.Id);

        var result = await controller.GetAdministrador(9999);

        var notFoundResult = result as NotFoundObjectResult;
        notFoundResult.Should().NotBeNull();
        notFoundResult!.StatusCode.Should().Be(404);
    }

    // ── POST /api/administradores ───────────────────────────────────────────

    [Fact]
    public async Task CreateUsuario_WithValidData_ReturnsCreated()
    {
        // Arrange
        var controller = CreateControllerWithUser(UserRoles.Gestor, _farmacia.Id);
        var request = new CreateUsuarioRequest
        {
            Nome = "Novo Atendente",
            Email = "novo@teste.com",
            Password = "Novo@123",
            Role = UserRoles.Atendente,
            FarmaciaId = _farmacia.Id,
            Turno = "Manhã"
        };

        // Act
        var result = await controller.CreateAdministrador(request);

        // Assert
        var createdResult = result as CreatedAtActionResult;
        createdResult.Should().NotBeNull();
        createdResult!.StatusCode.Should().Be(201);

        // Verify user was saved
        var savedUser = await _context.UsuariosFarmacia.FirstOrDefaultAsync(u => u.Email == "novo@teste.com");
        savedUser.Should().NotBeNull();
        savedUser!.Nome.Should().Be("Novo Atendente");
        savedUser.Role.Should().Be(UserRoles.Atendente);
    }

    [Fact]
    public async Task CreateUsuario_WithMissingFields_ReturnsBadRequest()
    {
        var controller = CreateControllerWithUser(UserRoles.Gestor, _farmacia.Id);
        var request = new CreateUsuarioRequest { Nome = "", Email = "", Password = "" };

        var result = await controller.CreateAdministrador(request);

        var badRequest = result as BadRequestObjectResult;
        badRequest.Should().NotBeNull();
        badRequest!.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task CreateUsuario_WithShortPassword_ReturnsBadRequest()
    {
        var controller = CreateControllerWithUser(UserRoles.Gestor, _farmacia.Id);
        var request = new CreateUsuarioRequest
        {
            Nome = "Test", Email = "short@teste.com",
            Password = "12345", Role = UserRoles.Atendente, FarmaciaId = _farmacia.Id
        };

        var result = await controller.CreateAdministrador(request);

        var badRequest = result as BadRequestObjectResult;
        badRequest.Should().NotBeNull();
        badRequest!.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task ResetPassword_WithShortPassword_ReturnsBadRequest()
    {
        var user = CreateTestUser("resetshort@teste.com", UserRoles.Atendente, _farmacia.Id);
        var controller = CreateControllerWithUser(UserRoles.Gestor, _farmacia.Id);
        var request = new ResetPasswordRequest { NewPassword = "abc" };

        var result = await controller.ResetPassword(user.Id, request);

        var badRequest = result as BadRequestObjectResult;
        badRequest.Should().NotBeNull();
        badRequest!.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task CreateUsuario_WithInvalidRole_ReturnsBadRequest()
    {
        var controller = CreateControllerWithUser(UserRoles.Gestor, _farmacia.Id);
        var request = new CreateUsuarioRequest
        {
            Nome = "Test", Email = "test@teste.com",
            Password = "Test@123", Role = "InvalidRole", FarmaciaId = _farmacia.Id
        };

        var result = await controller.CreateAdministrador(request);

        var badRequest = result as BadRequestObjectResult;
        badRequest.Should().NotBeNull();
        badRequest!.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task CreateUsuario_WithDuplicateEmail_ReturnsBadRequest()
    {
        // Arrange
        CreateTestUser("duplicate@teste.com", UserRoles.Atendente, _farmacia.Id);
        var controller = CreateControllerWithUser(UserRoles.Gestor, _farmacia.Id);
        var request = new CreateUsuarioRequest
        {
            Nome = "Duplicate", Email = "duplicate@teste.com",
            Password = "Test@123", Role = UserRoles.Atendente, FarmaciaId = _farmacia.Id
        };

        // Act
        var result = await controller.CreateAdministrador(request);

        // Assert
        var badRequest = result as BadRequestObjectResult;
        badRequest.Should().NotBeNull();
        badRequest!.StatusCode.Should().Be(400);
    }

    // ── PUT /api/administradores/{id} ───────────────────────────────────────

    [Fact]
    public async Task UpdateUsuario_WithValidData_ReturnsOk()
    {
        // Arrange
        var user = CreateTestUser("update@teste.com", UserRoles.Atendente, _farmacia.Id);
        var controller = CreateControllerWithUser(UserRoles.Gestor, _farmacia.Id);
        var request = new UpdateUsuarioRequest
        {
            Nome = "Nome Atualizado",
            Turno = "Noite"
        };

        // Act
        var result = await controller.UpdateAdministrador(user.Id, request);

        // Assert
        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);

        var updated = await _context.UsuariosFarmacia.FindAsync(user.Id);
        updated!.Nome.Should().Be("Nome Atualizado");
        updated.Turno.Should().Be("Noite");
    }

    [Fact]
    public async Task UpdateUsuario_NonExistentUser_ReturnsNotFound()
    {
        var controller = CreateControllerWithUser(UserRoles.Gestor, _farmacia.Id);
        var request = new UpdateUsuarioRequest { Nome = "Test" };

        var result = await controller.UpdateAdministrador(9999, request);

        var notFoundResult = result as NotFoundObjectResult;
        notFoundResult.Should().NotBeNull();
        notFoundResult!.StatusCode.Should().Be(404);
    }

    // ── DELETE /api/administradores/{id} ────────────────────────────────────

    [Fact]
    public async Task DeleteUsuario_ExistingUser_DeactivatesUser()
    {
        // Arrange
        var user = CreateTestUser("delete@teste.com", UserRoles.Atendente, _farmacia.Id);
        var controller = CreateControllerWithUser(UserRoles.Gestor, _farmacia.Id, userId: "999");

        // Act
        var result = await controller.DeleteAdministrador(user.Id);

        // Assert
        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);

        var deactivated = await _context.UsuariosFarmacia.FindAsync(user.Id);
        deactivated!.Ativo.Should().BeFalse();
        deactivated.DataInativacao.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteUsuario_SelfDeactivation_ReturnsBadRequest()
    {
        // Arrange
        var user = CreateTestUser("self@teste.com", UserRoles.Gestor, _farmacia.Id);
        var controller = CreateControllerWithUser(UserRoles.Gestor, _farmacia.Id, userId: user.Id.ToString());

        // Act
        var result = await controller.DeleteAdministrador(user.Id);

        // Assert
        var badRequest = result as BadRequestObjectResult;
        badRequest.Should().NotBeNull();
        badRequest!.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task DeleteUsuario_NonExistentUser_ReturnsNotFound()
    {
        var controller = CreateControllerWithUser(UserRoles.Gestor, _farmacia.Id);

        var result = await controller.DeleteAdministrador(9999);

        var notFoundResult = result as NotFoundObjectResult;
        notFoundResult.Should().NotBeNull();
        notFoundResult!.StatusCode.Should().Be(404);
    }

    // ── PUT /api/administradores/{id}/reset-password ────────────────────────

    [Fact]
    public async Task ResetPassword_WithValidData_ReturnsOk()
    {
        // Arrange
        var user = CreateTestUser("reset@teste.com", UserRoles.Atendente, _farmacia.Id);
        var oldHash = user.PasswordHash;
        var controller = CreateControllerWithUser(UserRoles.Gestor, _farmacia.Id);
        var request = new ResetPasswordRequest { NewPassword = "NewPass@123" };

        // Act
        var result = await controller.ResetPassword(user.Id, request);

        // Assert
        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);

        var updated = await _context.UsuariosFarmacia.FindAsync(user.Id);
        updated!.PasswordHash.Should().NotBe(oldHash);
    }

    [Fact]
    public async Task ResetPassword_WithEmptyPassword_ReturnsBadRequest()
    {
        var user = CreateTestUser("resetfail@teste.com", UserRoles.Atendente, _farmacia.Id);
        var controller = CreateControllerWithUser(UserRoles.Gestor, _farmacia.Id);
        var request = new ResetPasswordRequest { NewPassword = "" };

        var result = await controller.ResetPassword(user.Id, request);

        var badRequest = result as BadRequestObjectResult;
        badRequest.Should().NotBeNull();
        badRequest!.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task ResetPassword_NonExistentUser_ReturnsNotFound()
    {
        var controller = CreateControllerWithUser(UserRoles.Gestor, _farmacia.Id);
        var request = new ResetPasswordRequest { NewPassword = "Test@123" };

        var result = await controller.ResetPassword(9999, request);

        var notFoundResult = result as NotFoundObjectResult;
        notFoundResult.Should().NotBeNull();
        notFoundResult!.StatusCode.Should().Be(404);
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
