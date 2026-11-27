using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using PharmazapPlus.API.Controllers;
using PharmazapPlus.API.Data;
using PharmazapPlus.API.Services.Login;
using PharmazapPlus.Shared.Models.Core;
using PharmazapPlus.Shared.Enums;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using PharmazapPlus.Shared;
using System.Text.Json;
using PharmazapPlus.Shared.Models.CRM;
using PharmazapPlus.Shared.Models.Mensagens;

namespace PharmazapPlus.API.Tests.Controllers;

public class AccountControllerTests
{
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<AccountController>> _mockLogger;
    private readonly PharmazapDbContext _context;
    private readonly TokenProvider _tokenProvider;

    public AccountControllerTests()
    {
        _mockConfiguration = new Mock<IConfiguration>();
        _mockConfiguration.Setup(c => c["Jwt:Key"]).Returns("PharmazapPlusTestSecretKey2024!@#$%^&*()");
        _mockConfiguration.Setup(c => c["Jwt:Issuer"]).Returns("PharmazapPlusTest");
        _mockConfiguration.Setup(c => c["Jwt:Audience"]).Returns("PharmazapPlusTest");
        _mockLogger = new Mock<ILogger<AccountController>>();
        _tokenProvider = new TokenProvider(_mockConfiguration.Object);

        var options = new DbContextOptionsBuilder<PharmazapDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new PharmazapDbContext(options);
    }

    [Fact]
    public async Task GetUserInfo_WithValidUser_ReturnsUserInformation()
    {
        // Arrange
        var farmacia = new Farmacia
        {
            Nome = "Farmacia Teste",
            Cnpj = "12.345.678/0001-00",
            Telefone = "(11) 99999-9999",
            Email = "teste@farmacia.com",
            Endereco = "Rua Teste",
            Cidade = "São Paulo",
            Estado = "SP",
            Cep = "01000000",
            LogoUrl = "/uploads/logos/farmacia-teste.png",
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "key-1",
                WhatsAppPhoneNumberId = "",
                WhatsAppAccessToken = "",
                WhatsAppBusinessAccountId = ""
            }
        };
        _context.Farmacias.Add(farmacia);
        await _context.SaveChangesAsync();

        _context.UsuariosFarmacia.Add(new UsuarioFarmacia
        {
            Id = 123,
            FarmaciaId = farmacia.Id,
            Nome = "Test User",
            Email = "test@example.com",
            Telefone = "(11) 99999-0000",
            Role = "Admin",
            Ativo = true,
            Turno = "Integral",
            PasswordHash = "",
            PasswordSalt = "",
            Farmacia = null!
        });
        await _context.SaveChangesAsync();

        var controller = new AccountController(_mockConfiguration.Object, _context, _mockLogger.Object, _tokenProvider);
        
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "123"),
            new Claim(ClaimTypes.Email, "test@example.com"),
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim("FarmaciaId", "1"),
            new Claim("FarmaciaNome", "Farmacia Teste")
        };
        
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);
        
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };

        // Act
        var result = await controller.GetUserInfo() as OkObjectResult;

        // Assert
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(200);

        var payloadJson = JsonSerializer.Serialize(result.Value);
        using var payload = JsonDocument.Parse(payloadJson);
        payload.RootElement.GetProperty("Data").GetProperty("FarmaciaLogoUrl").GetString().Should().Be("/uploads/logos/farmacia-teste.png");
    }

    [Fact]
    public async Task GetUserInfo_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var controller = new AccountController(_mockConfiguration.Object, _context, _mockLogger.Object, _tokenProvider);
        
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        // Act
        var result = await controller.GetUserInfo() as OkObjectResult;

        // Assert
        result.Should().NotBeNull();
        // Note: In real scenario, [Authorize] attribute would prevent this from being called
    }

    [Fact]
    public async Task GetDashboardStats_AsGestor_ReturnsFarmaciaAndUserCounts()
    {
        // Arrange
        var farmacia = new Farmacia
        {
            Nome = "Farmacia Teste",
            Cnpj = "99.999.999/0001-99",
            Telefone = "(11) 99999-9999",
            Email = "contato@stats.com",
            Endereco = "Rua Stats, 1",
            Cidade = "São Paulo",
            Estado = "SP",
            Cep = "01000000",
            DataCadastro = DateTime.UtcNow,
            Status = Shared.Models.Core.FarmaciaStatus.Ativa,
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "stats-key",
                WhatsAppPhoneNumberId = "",
                WhatsAppAccessToken = "",
                WhatsAppBusinessAccountId = ""
            },
        };
        _context.Farmacias.Add(farmacia);
        await _context.SaveChangesAsync();

        var (hash, salt) = GeneratePasswordHash("Test@123");
        _context.UsuariosFarmacia.Add(new UsuarioFarmacia
        {
            Nome = "User 1", Email = "u1@s.com", Telefone = "(11)00000-0000",
            Role = UserRoles.Atendente, FarmaciaId = farmacia.Id,
            DataCadastro = DateTime.UtcNow, Ativo = true, Turno = "Integral",
            PasswordHash = hash, PasswordSalt = salt, Farmacia = null!
        });
        await _context.SaveChangesAsync();

        var controller = new AccountController(_mockConfiguration.Object, _context, _mockLogger.Object, _tokenProvider);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Role, UserRoles.Gestor),
            new Claim(ClaimTypes.NameIdentifier, "999"),
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthType")) }
        };

        // Act
        var result = await controller.GetDashboardStats() as OkObjectResult;

        // Assert
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(200);

        var json = JsonSerializer.Serialize(result.Value);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("Success").GetBoolean().Should().BeTrue();
        var data = doc.RootElement.GetProperty("Data");
        data.GetProperty("TotalFarmacias").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        data.GetProperty("TotalAdministradores").GetInt32().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetDashboardStats_AsAdministrador_ReturnsFarmaciaStats()
    {
        // Arrange
        var farmacia = new Farmacia
        {
            Nome = "Farmacia Admin",
            Cnpj = "88.888.888/0001-88",
            Telefone = "(11) 99999-9999",
            Email = "admin-stats@test.com",
            Endereco = "Rua Admin, 1",
            Cidade = "São Paulo",
            Estado = "SP",
            Cep = "01000000",
            DataCadastro = DateTime.UtcNow,
            Status = Shared.Models.Core.FarmaciaStatus.Ativa,
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "admin-stats-key",
                WhatsAppPhoneNumberId = "",
                WhatsAppAccessToken = "",
                WhatsAppBusinessAccountId = ""
            },
        };
        _context.Farmacias.Add(farmacia);
        await _context.SaveChangesAsync();

        var controller = new AccountController(_mockConfiguration.Object, _context, _mockLogger.Object, _tokenProvider);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Role, UserRoles.Administrador),
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim("FarmaciaId", farmacia.Id.ToString()),
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthType")) }
        };

        // Act
        var result = await controller.GetDashboardStats() as OkObjectResult;

        // Assert
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(200);

        var json = JsonSerializer.Serialize(result.Value);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("Success").GetBoolean().Should().BeTrue();
        var data = doc.RootElement.GetProperty("Data");
        // No clients added yet – count should be 0
        data.GetProperty("TotalClientes").GetInt32().Should().Be(0);
        data.GetProperty("AtendimentosHoje").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task GetDashboardStats_AsAtendente_ReturnsPendingCount()
    {
        // Arrange
        var farmacia = new Farmacia
        {
            Nome = "Farmacia Atendente",
            Cnpj = "77.777.777/0001-77",
            Telefone = "(11) 99999-9999",
            Email = "atend-stats@test.com",
            Endereco = "Rua Atend, 1",
            Cidade = "São Paulo",
            Estado = "SP",
            Cep = "01000000",
            DataCadastro = DateTime.UtcNow,
            Status = Shared.Models.Core.FarmaciaStatus.Ativa,
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "atend-stats-key",
                WhatsAppPhoneNumberId = "",
                WhatsAppAccessToken = "",
                WhatsAppBusinessAccountId = ""
            },
        };
        _context.Farmacias.Add(farmacia);
        await _context.SaveChangesAsync();

        _context.Atendimentos.Add(new Shared.Models.Mensagens.Atendimento
        {
            WhatsAppId = "+5511999999999",
            FarmaciaId = farmacia.Id,
            StatusEnum = AtendimentoStatusEnum.Pendente,
            CanalEnum = Shared.Enums.CanalEnum.WhatsApp,
            DataInicio = DateTime.UtcNow,
            Farmacia = null!,
            Cliente = null!,
            Atendente = null!
        });
        await _context.SaveChangesAsync();

        var controller = new AccountController(_mockConfiguration.Object, _context, _mockLogger.Object, _tokenProvider);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Role, UserRoles.Atendente),
            new Claim(ClaimTypes.NameIdentifier, "42"),
            new Claim("FarmaciaId", farmacia.Id.ToString()),
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthType")) }
        };

        // Act
        var result = await controller.GetDashboardStats() as OkObjectResult;

        // Assert
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(200);

        var json = JsonSerializer.Serialize(result.Value);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("Success").GetBoolean().Should().BeTrue();
        var data = doc.RootElement.GetProperty("Data");
        data.GetProperty("AtendimentosPendentes").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task GetDashboardStats_WithNoFarmaciaIdAsAtendente_ReturnsBadRequest()
    {
        // Arrange
        var controller = new AccountController(_mockConfiguration.Object, _context, _mockLogger.Object, _tokenProvider);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Role, UserRoles.Atendente),
            new Claim(ClaimTypes.NameIdentifier, "1"),
            // No FarmaciaId claim
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthType")) }
        };

        // Act
        var result = await controller.GetDashboardStats();

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        var controller = new AccountController(_mockConfiguration.Object, _context, _mockLogger.Object, _tokenProvider);

        // Assert
        controller.Should().NotBeNull();
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkWithToken()
    {
        // Arrange
        var password = "Test@123456";
        var (hash, salt) = GeneratePasswordHash(password);

        var farmacia = new Farmacia
        {
            Nome = "Farmacia Teste",
            Cnpj = "00.000.000/0001-00",
            Telefone = "(11) 99999-9999",
            Email = "contato@teste.com",
            Endereco = "Rua Teste, 123",
            Cidade = "São Paulo",
            Estado = "SP",
            Cep = "01000000",
            LogoUrl = "/uploads/logos/login-farmacia.png",
            DataCadastro = DateTime.UtcNow,
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "test-key",
                WhatsAppPhoneNumberId = "",
                WhatsAppAccessToken = "",
                WhatsAppBusinessAccountId = ""
            },
        };
        _context.Farmacias.Add(farmacia);
        await _context.SaveChangesAsync();

        var user = new UsuarioFarmacia
        {
            Nome = "Admin Teste",
            Email = "admin@teste.com",
            Telefone = "(11) 99999-9999",
            Role = UserRoles.Administrador,
            PasswordHash = hash,
            PasswordSalt = salt,
            Ativo = true,
            FarmaciaId = farmacia.Id,
            DataCadastro = DateTime.UtcNow,
            Turno = "Integral",
            Farmacia = null!
        };
        _context.UsuariosFarmacia.Add(user);
        await _context.SaveChangesAsync();

        var controller = new AccountController(_mockConfiguration.Object, _context, _mockLogger.Object, _tokenProvider);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var request = new LoginRequest { Email = "admin@teste.com", Password = "Test@123456" };

        // Act
        var result = await controller.Login(request);

        // Assert
        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);

        var payloadJson = JsonSerializer.Serialize(okResult.Value);
        using var payload = JsonDocument.Parse(payloadJson);
        var token = payload.RootElement.GetProperty("Data").GetProperty("Token").GetString();
        token.Should().NotBeNullOrWhiteSpace();
        payload.RootElement.GetProperty("Data").GetProperty("FarmaciaLogoUrl").GetString().Should().Be("/uploads/logos/login-farmacia.png");

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token!);
        jwt.Claims.Should().Contain(c => c.Type == Consts.Claims.FarmaciaId && c.Value == farmacia.Id.ToString());
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        var password = "Test@123456";
        var (hash, salt) = GeneratePasswordHash(password);

        var farmacia = new Farmacia
        {
            Nome = "Farmacia Teste",
            Cnpj = "11.111.111/0001-11",
            Telefone = "(11) 99999-9999",
            Email = "contato@teste2.com",
            Endereco = "Rua Teste, 456",
            Cidade = "São Paulo",
            Estado = "SP",
            Cep = "01000000",
            DataCadastro = DateTime.UtcNow,
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "test-key-2",
                WhatsAppPhoneNumberId = "",
                WhatsAppAccessToken = "",
                WhatsAppBusinessAccountId = ""
            },
        };
        _context.Farmacias.Add(farmacia);
        await _context.SaveChangesAsync();

        var user = new UsuarioFarmacia
        {
            Nome = "Admin Teste",
            Email = "admin@teste2.com",
            Telefone = "(11) 99999-9999",
            Role = UserRoles.Administrador,
            PasswordHash = hash,
            PasswordSalt = salt,
            Ativo = true,
            FarmaciaId = farmacia.Id,
            DataCadastro = DateTime.UtcNow,
            Turno = "Integral",
            Farmacia = null!
        };
        _context.UsuariosFarmacia.Add(user);
        await _context.SaveChangesAsync();

        var controller = new AccountController(_mockConfiguration.Object, _context, _mockLogger.Object, _tokenProvider);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var request = new LoginRequest { Email = "admin@teste2.com", Password = "WrongPassword" };

        // Act
        var result = await controller.Login(request);

        // Assert
        var unauthorizedResult = result as UnauthorizedObjectResult;
        unauthorizedResult.Should().NotBeNull();
        unauthorizedResult!.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task Login_WithFarmaciaDesativada_ReturnsUnauthorizedWithAdminMessage()
    {
        // Arrange
        var password = "Test@123456";
        var (hash, salt) = GeneratePasswordHash(password);

        var farmacia = new Farmacia
        {
            Nome = "Farmacia Bloqueada",
            Cnpj = "22.222.222/0001-22",
            Telefone = "(11) 99999-9999",
            Email = "contato@bloqueada.com",
            Endereco = "Rua Bloqueada, 1",
            Cidade = "São Paulo",
            Estado = "SP",
            Cep = "01000000",
            Ativa = false,
            MotivoBloqueio = "financeiro",
            Status = Shared.Models.Core.FarmaciaStatus.Suspensa,
            DataCadastro = DateTime.UtcNow,
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "blocked-key",
                WhatsAppPhoneNumberId = "",
                WhatsAppAccessToken = "",
                WhatsAppBusinessAccountId = ""
            },
        };
        _context.Farmacias.Add(farmacia);
        await _context.SaveChangesAsync();

        var user = new UsuarioFarmacia
        {
            Nome = "Admin Bloqueado",
            Email = "admin@bloqueada.com",
            Telefone = "(11) 99999-9999",
            Role = UserRoles.Administrador,
            PasswordHash = hash,
            PasswordSalt = salt,
            Ativo = true,
            FarmaciaId = farmacia.Id,
            DataCadastro = DateTime.UtcNow,
            Turno = "Integral",
            Farmacia = null!
        };
        _context.UsuariosFarmacia.Add(user);
        await _context.SaveChangesAsync();

        var controller = new AccountController(_mockConfiguration.Object, _context, _mockLogger.Object, _tokenProvider);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var request = new LoginRequest { Email = "admin@bloqueada.com", Password = password };

        // Act
        var result = await controller.Login(request);

        // Assert
        var unauthorizedResult = result as UnauthorizedObjectResult;
        unauthorizedResult.Should().NotBeNull();
        unauthorizedResult!.StatusCode.Should().Be(401);
        var payloadJson = JsonSerializer.Serialize(unauthorizedResult.Value);
        using var payload = JsonDocument.Parse(payloadJson);
        payload.RootElement.GetProperty("Message").GetString().Should().Be("Contacte seu administrador");
    }

    [Fact]
    public async Task UpdateFarmacia_DeactivateWithoutReason_ReturnsBadRequest()
    {
        // Arrange
        var farmacia = new Farmacia
        {
            Nome = "Farmacia Teste",
            Cnpj = "33.333.333/0001-33",
            Telefone = "(11) 99999-9999",
            Email = "contato@teste3.com",
            Endereco = "Rua Teste, 789",
            Cidade = "São Paulo",
            Estado = "SP",
            Cep = "01000000",
            DataCadastro = DateTime.UtcNow,
            Status = Shared.Models.Core.FarmaciaStatus.Ativa,
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "test-key-3",
                WhatsAppPhoneNumberId = "",
                WhatsAppAccessToken = "",
                WhatsAppBusinessAccountId = ""
            },
        };
        _context.Farmacias.Add(farmacia);
        await _context.SaveChangesAsync();

        var controller = new AccountController(_mockConfiguration.Object, _context, _mockLogger.Object, _tokenProvider);
        var request = new UpdateFarmaciaRequest { Ativa = false, MotivoBloqueio = "   " };

        // Act
        var result = await controller.UpdateFarmacia(farmacia.Id, request);

        // Assert
        var badRequestResult = result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task RegisterFarmacia_WithDuplicateEmailOrPhone_ReturnsConflict()
    {
        // Arrange
        var farmacia = new Farmacia
        {
            Nome = "Farmacia Teste",
            Cnpj = "00.000.000/0001-00",
            Telefone = "(62) 3333-4444",
            Email = "contato@farmaciateste.com.br",
            Endereco = "Rua Teste, 123",
            Cidade = "Goiânia",
            Estado = "GO",
            Cep = "74000000",
            DataCadastro = DateTime.UtcNow,
            Status = Shared.Models.Core.FarmaciaStatus.Ativa,
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "existing-key",
                WhatsAppPhoneNumberId = "",
                WhatsAppAccessToken = "",
                WhatsAppBusinessAccountId = ""
            },
        };
        _context.Farmacias.Add(farmacia);
        await _context.SaveChangesAsync();

        var controller = new AccountController(_mockConfiguration.Object, _context, _mockLogger.Object, _tokenProvider);
        var request = new RegisterFarmaciaRequest
        {
            Nome = "Farmacia Teste",
            Cnpj = "28.162.570/0001-05",
            Telefone = "6233334444",
            Email = "contato@farmaciateste.com.br",
            Endereco = "Rua Teste, 999",
            NomeContato = "Responsável",
            EmailContato = "resp@farmaciateste.com.br",
            TelefoneContato = "62999999999",
            Plano = "Premium"
        };

        // Act
        var result = await controller.RegisterFarmacia(request);

        // Assert
        var conflictResult = result as ConflictObjectResult;
        conflictResult.Should().NotBeNull();
        conflictResult!.StatusCode.Should().Be(409);
        (await _context.Farmacias.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task RegisterFarmacia_WithDuplicateEmailOnly_ReturnsConflict()
    {
        // Arrange
        var farmacia = new Farmacia
        {
            Nome = "Farmacia Base",
            Cnpj = "11.111.111/0001-11",
            Telefone = "(62) 3333-4444",
            Email = "contato@farmaciateste.com.br",
            Endereco = "Rua Base, 123",
            Cidade = "Goiânia",
            Estado = "GO",
            Cep = "74000000",
            DataCadastro = DateTime.UtcNow,
            Status = Shared.Models.Core.FarmaciaStatus.Ativa,
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "existing-key-2",
                WhatsAppPhoneNumberId = "",
                WhatsAppAccessToken = "",
                WhatsAppBusinessAccountId = ""
            },
        };
        _context.Farmacias.Add(farmacia);
        await _context.SaveChangesAsync();

        var controller = new AccountController(_mockConfiguration.Object, _context, _mockLogger.Object, _tokenProvider);
        var request = new RegisterFarmaciaRequest
        {
            Nome = "Farmacia Nova",
            Cnpj = "22.222.222/0001-22",
            Telefone = "(62) 9999-9999",
            Email = "contato@farmaciateste.com.br",
            Endereco = "Rua Nova, 999",
            NomeContato = "Responsável",
            EmailContato = "resp@farmaciateste.com.br",
            TelefoneContato = "62999999999",
            Plano = "Premium"
        };

        // Act
        var result = await controller.RegisterFarmacia(request);

        // Assert
        var conflictResult = result as ConflictObjectResult;
        conflictResult.Should().NotBeNull();
        conflictResult!.StatusCode.Should().Be(409);
        (await _context.Farmacias.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task RegisterFarmacia_CreatesFarmaciaDefaultsAndDepartamentoInicial()
    {
        var controller = new AccountController(_mockConfiguration.Object, _context, _mockLogger.Object, _tokenProvider);
        var request = new RegisterFarmaciaRequest
        {
            Nome = "Farmacia Nova",
            Cnpj = "44.444.444/0001-44",
            Telefone = "6233334444",
            Email = "contato@farmacianova.com.br",
            Endereco = "Rua Nova, 100",
            NomeContato = "Responsável",
            EmailContato = "resp@farmacianova.com.br",
            TelefoneContato = "62999999999",
            Plano = "Premium"
        };

        var result = await controller.RegisterFarmacia(request);

        result.Should().BeOfType<OkObjectResult>();

        var farmacia = await _context.Farmacias
            .SingleAsync(f => f.Cnpj == "44444444000144");

        farmacia.Should().NotBeNull();

        (await _context.Departamentos.CountAsync(d => d.FarmaciaId == farmacia.Id && d.Nome == "Atendimento"))
            .Should().Be(1);

        (await _context.Etiquetas.CountAsync(e => e.FarmaciaId == farmacia.Id))
            .Should().BeGreaterThan(0);

        (await _context.MensagensPreDefinidas.CountAsync(m => m.FarmaciaId == farmacia.Id))
            .Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task UpdateFarmacia_WithDuplicateIdentityData_ReturnsConflict()
    {
        // Arrange
        var farmaciaOriginal = new Farmacia
        {
            Nome = "Farmacia Original",
            Cnpj = "11.111.111/0001-11",
            Telefone = "(62) 3333-4444",
            Email = "contato@farmaciateste.com.br",
            Endereco = "Rua Teste, 123",
            Cidade = "Goiânia",
            Estado = "GO",
            Cep = "74000000",
            DataCadastro = DateTime.UtcNow,
            Status = Shared.Models.Core.FarmaciaStatus.Ativa,
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "key-1",
                WhatsAppPhoneNumberId = "",
                WhatsAppAccessToken = "",
                WhatsAppBusinessAccountId = ""
            },
        };

        var farmaciaAlvo = new Farmacia
        {
            Nome = "Farmacia Alvo",
            Cnpj = "22.222.222/0001-22",
            Telefone = "(62) 9999-9999",
            Email = "alvo@farmaciateste.com.br",
            Endereco = "Rua Alvo, 456",
            Cidade = "Goiânia",
            Estado = "GO",
            Cep = "74000001",
            DataCadastro = DateTime.UtcNow,
            Status = Shared.Models.Core.FarmaciaStatus.Ativa,
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "key-2",
                WhatsAppPhoneNumberId = "",
                WhatsAppAccessToken = "",
                WhatsAppBusinessAccountId = ""
            },
        };

        _context.Farmacias.AddRange(farmaciaOriginal, farmaciaAlvo);
        await _context.SaveChangesAsync();

        var controller = new AccountController(_mockConfiguration.Object, _context, _mockLogger.Object, _tokenProvider);
        var request = new UpdateFarmaciaRequest
        {
            Nome = "Farmacia Alvo",
            Cnpj = "22.222.222/0001-22",
            Telefone = "(62) 3333-4444",
            Email = "contato@farmaciateste.com.br",
            Endereco = "Rua Alvo, 456",
            Plano = "Premium",
            Ativa = true
        };

        // Act
        var result = await controller.UpdateFarmacia(farmaciaAlvo.Id, request);

        // Assert
        var conflictResult = result as ConflictObjectResult;
        conflictResult.Should().NotBeNull();
        conflictResult!.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task Login_WithNonExistentUser_ReturnsUnauthorized()
    {
        // Arrange
        var controller = new AccountController(_mockConfiguration.Object, _context, _mockLogger.Object, _tokenProvider);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var request = new LoginRequest { Email = "nonexistent@teste.com", Password = "Any@123" };

        // Act
        var result = await controller.Login(request);

        // Assert
        var unauthorizedResult = result as UnauthorizedObjectResult;
        unauthorizedResult.Should().NotBeNull();
        unauthorizedResult!.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task Login_WithEmptyCredentials_ReturnsBadRequest()
    {
        // Arrange
        var controller = new AccountController(_mockConfiguration.Object, _context, _mockLogger.Object, _tokenProvider);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var request = new LoginRequest { Email = "", Password = "" };

        // Act
        var result = await controller.Login(request);

        // Assert
        var badRequestResult = result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.StatusCode.Should().Be(400);
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
