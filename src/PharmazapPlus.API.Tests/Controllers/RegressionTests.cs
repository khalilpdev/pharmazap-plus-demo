using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using PharmazapPlus.API.Controllers;
using PharmazapPlus.API.Data;
using PharmazapPlus.API.Services.CRM;
using PharmazapPlus.API.Services.Security;
using PharmazapPlus.Shared.Models.CRM;
using PharmazapPlus.Shared.Models.Core;
using FluentAssertions;
using System.Security.Claims;

namespace PharmazapPlus.API.Tests.Controllers;

/// <summary>
/// Regression tests for previously fixed issues to prevent reoccurrence.
/// </summary>
public class RegressionTests
{
    #region EF Core Migration - Tuple Return Type

    /// <summary>
    /// Regression test: ICrmService.DeleteCliente returns (bool Success, string Message) tuple.
    /// Mocking with ReturnsAsync(true) causes CS1929 error. Must use ReturnsAsync((true, string.Empty)).
    /// See: AGENTS.md > Fixed Issues > Test Mock Returns
    /// </summary>
    [Fact]
    public async Task DeleteCliente_WithTupleReturn_MustUseCorrectReturnsAsync()
    {
        // Arrange
        var mockCrmService = new Mock<ICrmService>();
        var mockLogger = new Mock<ILogger<ClientesController>>();
        var controller = new ClientesController(mockCrmService.Object, mockLogger.Object);

        var claims = new[] { new Claim("FarmaciaId", "1") };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };

        // ✅ Correct way - tuple return
        mockCrmService
            .Setup(s => s.DeleteCliente(1, "1"))
            .ReturnsAsync((true, string.Empty));

        // Act
        var result = await controller.DeleteCliente(1);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    /// <summary>
    /// Regression test: Same pattern applies to ReativarCliente which also returns (bool, string).
    /// </summary>
    [Fact]
    public async Task ReativarCliente_WithTupleReturn_MustUseCorrectReturnsAsync()
    {
        // Arrange
        var mockCrmService = new Mock<ICrmService>();
        var mockLogger = new Mock<ILogger<ClientesController>>();
        var controller = new ClientesController(mockCrmService.Object, mockLogger.Object);

        var claims = new[] { new Claim("FarmaciaId", "1") };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };

        mockCrmService
            .Setup(s => s.ReativarCliente(1, "1"))
            .ReturnsAsync((true, string.Empty));

        // Act & Assert - should not throw CS1929
        var result = await controller.ReativarCliente(1);
        result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region Launch Configuration

    /// <summary>
    /// Regression test: Verify launch.json does not contain invalid properties for coreclr type.
    /// Invalid properties like browserUrl, runtimeExecutable, runtimeArgs cause VS Code warnings.
    /// See: AGENTS.md > Fixed Issues > Launch Configuration
    /// </summary>
    [Fact]
    public void LaunchJson_WebProfile_ShouldNotHaveInvalidProperties()
    {
        // Arrange
        var launchJsonPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..", "..",
            ".vscode", "launch.json");

        if (!File.Exists(launchJsonPath))
            return; // Skip if file doesn't exist in test environment

        var jsonContent = File.ReadAllText(launchJsonPath);
        var jsonDoc = System.Text.Json.JsonDocument.Parse(jsonContent);

        // Act & Assert
        if (jsonDoc.RootElement.TryGetProperty("configurations", out var configs))
        {
            foreach (var config in configs.EnumerateArray())
            {
                if (config.TryGetProperty("name", out var nameProp) &&
                    nameProp.GetString() == "WEB")
                {
                    // browserUrl should NOT exist
                    config.TryGetProperty("browserUrl", out _).Should().BeFalse(
                        "browserUrl is not allowed in coreclr type launch config");

                    // runtimeExecutable should NOT exist
                    config.TryGetProperty("runtimeExecutable", out _).Should().BeFalse(
                        "runtimeExecutable is not allowed in coreclr type launch config");

                    // runtimeArgs should NOT exist
                    config.TryGetProperty("runtimeArgs", out _).Should().BeFalse(
                        "runtimeArgs is not allowed in coreclr type launch config");

                    // console should be valid value
                    if (config.TryGetProperty("console", out var consoleProp))
                    {
                        var consoleValue = consoleProp.GetString();
                        consoleValue.Should().BeOneOf(
                            "internalConsole", "integratedTerminal", "externalTerminal",
                            "console must have a valid value");
                    }
                }

                if (config.TryGetProperty("name", out var apiName) &&
                    apiName.GetString() == "API")
                {
                    // API profile should NOT have /api-docs in uriFormat
                    if (config.TryGetProperty("serverReadyAction", out var sra))
                    {
                        if (sra.TryGetProperty("uriFormat", out var uriFmt))
                        {
                            uriFmt.GetString().Should().NotContain(
                                "/api-docs",
                                "API profile should not have /api-docs in uriFormat");
                        }
                    }
                }
            }
        }
    }

    #endregion

    #region DatabaseSeeder — ChaveWhatsApp Overwrite Protection

    /// <summary>
    /// Regression test: DatabaseSeeder MUST NOT overwrite FarmaciaIntegracoes.ChaveWhatsApp
    /// on existing records when the value is already a real instance name (not the seed value).
    /// See: AGENTS.md > Fixed Issues > DatabaseSeeder não pode sobrescrever ChaveWhatsApp
    /// </summary>
    [Fact]
    public async Task DatabaseSeeder_MustNotOverwriteExistingChaveWhatsApp()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PharmazapDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;
        var context = new PharmazapDbContext(options);
        context.Database.EnsureCreated();

        var cryptoService = CryptoService.FromBase64Key(CryptoService.GenerateBase64Key());
        var logger = new Mock<ILogger>().Object;
        var config = new ConfigurationBuilder().Build();

        // Create a farmacia matching the seed CNPJ with existing integracoes
        // that already have a REAL ChaveWhatsApp (e.g., "5562998808389" — NOT the seed value)
        var farmacia = new Farmacia
        {
            Nome = "Farmacia Teste",
            Cnpj = "09717241000120",
            Email = "contato@farmaciateste.com.br",
            Telefone = "(62) 3333-4444",
            Endereco = "Rua das Flores, 123 - Centro",
            Cidade = "Goiânia",
            Estado = "GO",
            Cep = "74335-040",
            Status = FarmaciaStatus.Ativa,
            Plano = "Premium",
            DataCadastro = DateTime.UtcNow,
            DataAtivacao = DateTime.UtcNow,
            Ativa = true,
            IsActive = true,
            LimiteAtendentes = 10,
            LimiteClientes = 5000,
            CreatedAt = DateTime.UtcNow,
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "5562998808389", // real instance name, NOT seed value
                WhatsAppApiUrl = "http://localhost:8080",
                WhatsAppApiKey = "real-api-key",
                CreatedAt = DateTime.UtcNow
            }
        };
        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var farmaciaId = farmacia.Id;

        // Act — simulate restart/deploy by calling SeedAsync
        await DatabaseSeeder.SeedAsync(context, logger, cryptoService, config);

        // Assert — ChaveWhatsApp must remain the real value, NOT overwritten by seed
        var integracoes = await context.FarmaciaIntegracoes
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.FarmaciaId == farmaciaId);
        Assert.NotNull(integracoes);
        Assert.Equal("5562998808389", integracoes.ChaveWhatsApp);
    }

    #endregion
}
