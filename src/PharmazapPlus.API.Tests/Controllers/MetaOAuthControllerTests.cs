using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using PharmazapPlus.API.Controllers;
using PharmazapPlus.API.Data;
using PharmazapPlus.API.Services.MetaOAuth;
using PharmazapPlus.Shared.Enums;
using Xunit;

namespace PharmazapPlus.API.Tests.Controllers;

public class MetaOAuthControllerTests
{
    private static MetaOAuthController CreateController(IMetaOAuthService service)
    {
        var dbContext = new PharmazapDbContext(new DbContextOptionsBuilder<PharmazapDbContext>()
            .UseInMemoryDatabase($"MetaOAuthControllerTests_{Guid.NewGuid()}")
            .Options);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Application:WebUrl"] = "https://localhost:7160"
            })
            .Build();

        var logger = new LoggerFactory().CreateLogger<MetaOAuthController>();
        return new MetaOAuthController(service, dbContext, configuration, logger);
    }

    [Fact]
    public async Task Callback_WhenAuthorizationSucceeds_AutoClosesPopup()
    {
        var service = new Mock<IMetaOAuthService>();
        service.Setup(x => x.CompleteAuthorizationAsync("code", "state"))
            .ReturnsAsync(new MetaOAuthConnectionResult(true, "Conexao concluida.", "token", null, null, null, null, null, null));

        var controller = CreateController(service.Object);

        var result = await controller.Callback("code", "state", null, null);

        var content = Assert.IsType<ContentResult>(result).Content;
        Assert.Contains("window.close();", content);
        Assert.Contains("if (true)", content);
    }

    [Fact]
    public async Task Callback_WhenAuthorizationFails_KeepsPopupOpen()
    {
        var service = new Mock<IMetaOAuthService>();
        service.Setup(x => x.CompleteAuthorizationAsync("code", "state"))
            .ReturnsAsync(new MetaOAuthConnectionResult(false, "Falha ao conectar.", null, null, null, null, null, null, null, "error"));

        var controller = CreateController(service.Object);

        var result = await controller.Callback("code", "state", null, null);

        var content = Assert.IsType<ContentResult>(result).Content;
        Assert.Contains("Falha na conexão", content);
        Assert.Contains("if (false)", content);
        Assert.Contains("Fechar janela", content);
    }

    [Fact]
    public async Task Start_WhenMetaAppModeIsInvalid_ReturnsBadRequest()
    {
        var service = new Mock<IMetaOAuthService>();
        var controller = CreateController(service.Object);

        var request = new MetaOAuthStartHttpRequest(
            MetaOAuthChannelType.WhatsApp,
            MetaConfigSource.PharmazapCentral,
            (MetaAppMode)99);

        var result = await controller.Start(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Ambiente Meta inválido", badRequest.Value?.ToString());
        service.Verify(x => x.CreateAuthorizationUrlAsync(It.IsAny<MetaOAuthStartRequest>()), Times.Never);
    }

    [Fact]
    public async Task Start_WhenOwnConfigMissingSecret_ReturnsBadRequest()
    {
        var service = new Mock<IMetaOAuthService>();
        var controller = CreateController(service.Object);
        var request = new MetaOAuthStartHttpRequest(
            MetaOAuthChannelType.Instagram,
            MetaConfigSource.Own,
            MetaAppMode.Production,
            AppId: "own-app-id",
            AppSecret: null);

        var result = await controller.Start(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Meta App Secret é obrigatório", badRequest.Value?.ToString());
        service.Verify(x => x.CreateAuthorizationUrlAsync(It.IsAny<MetaOAuthStartRequest>()), Times.Never);
    }
}
