using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using PharmazapPlus.API.Data;
using PharmazapPlus.API.Hubs;
using PharmazapPlus.API.Services.CRM;
using PharmazapPlus.API.Services.Instagram;
using PharmazapPlus.API.Services.Queue;
using PharmazapPlus.API.Services.Security;
using PharmazapPlus.Shared.Enums;
using PharmazapPlus.Shared.Models.Config;
using PharmazapPlus.Shared.Models.Core;
using PharmazapPlus.Shared.Models.CRM;

namespace PharmazapPlus.API.Tests.Services;

public class InstagramServiceTests
{
    private static InstagramService CreateService(
        PharmazapDbContext context,
        Mock<ICrmService> crmServiceMock)
    {
        var cacheMock = new Mock<IDistributedCache>();
        var loggerMock = new Mock<ILogger<InstagramService>>();
        var queueMock = new Mock<IMessageQueue>();
        var hubContextMock = new Mock<IHubContext<PharmaHub>>();
        var hubClientsMock = new Mock<IHubClients>();
        var clientProxyMock = new Mock<IClientProxy>();
        var cryptoServiceMock = new Mock<ICryptoService>();

        clientProxyMock
            .Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        hubClientsMock.Setup(x => x.Group(It.IsAny<string>())).Returns(clientProxyMock.Object);
        hubContextMock.Setup(x => x.Clients).Returns(hubClientsMock.Object);

        queueMock
            .Setup(x => x.EnqueueAsync(It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        var config = Options.Create(new InstagramConfig
        {
            ApiBaseUrl = "https://graph.instagram.com",
            ApiVersion = "v21.0",
            WebhookVerifyToken = "test-token",
            DevelopmentMode = true,
            MockMessages = true
        });

        return new InstagramService(
            new HttpClient(),
            context,
            cacheMock.Object,
            loggerMock.Object,
            config,
            queueMock.Object,
            hubContextMock.Object,
            crmServiceMock.Object,
            cryptoServiceMock.Object);
    }

    [Fact]
    public async Task ProcessWebhookPayload_ResolvesFarmaciaFromChannelTable()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PharmazapDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        await using var context = new PharmazapDbContext(options);

        var farmacia = new Farmacia
        {
            Nome = "Farmacia Teste",
            Cnpj = "28162570000105",
            Telefone = "6233334444",
            Email = "contato@farmaciateste.com.br",
            Endereco = "Rua Teste",
            Cidade = "Goiânia",
            Estado = "GO",
            Cep = "74000000",
            DataCadastro = DateTime.UtcNow,
            Status = FarmaciaStatus.Ativa,
            Ativa = true,
            Plano = "Premium",
            Integracoes = new FarmaciaIntegracoes
            {
                ChaveWhatsApp = "key-1",
                WhatsAppPhoneNumberId = "",
                WhatsAppAccessToken = "",
                WhatsAppBusinessAccountId = ""
            }
        };

        context.Farmacias.Add(farmacia);
        await context.SaveChangesAsync();

        var canal = new Canal
        {
            Nome = "Instagram",
            Tipo = (int)CanalEnum.Instagram,
            IsOfficial = false,
            InstagramAccountId = "ig-recipient-1",
            FarmaciaIntegracosId = farmacia.Integracoes!.Id,
            FarmaciaIntegracoes = farmacia.Integracoes
        };
        context.Canais.Add(canal);
        await context.SaveChangesAsync();

        var cliente = new Cliente
        {
            FarmaciaId = farmacia.Id,
            Farmacia = farmacia,
            WhatsAppId = "sender-1",
            Nome = "Cliente Instagram",
            CodigoCliente = "CLI-0001"
        };

        var crmServiceMock = new Mock<ICrmService>();
        crmServiceMock
            .Setup(x => x.GetOrCreateClienteByCanalContato(farmacia.Id.ToString(), "sender-1", CanalEnum.Instagram, null))
            .ReturnsAsync(cliente);

        var service = CreateService(context, crmServiceMock);

        var payloadJson = JsonSerializer.Serialize(new
        {
            @object = "instagram",
            entry = new[]
            {
                new
                {
                    messaging = new[]
                    {
                        new
                        {
                            sender = new { id = "sender-1" },
                            recipient = new { id = "ig-recipient-1" },
                            message = new { text = "Oi" }
                        }
                    }
                }
            }
        });

        var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(payloadJson)!;

        // Act
        var result = await service.ProcessWebhookPayload(payload);

        // Assert
        result.Success.Should().BeTrue();
        context.Atendimentos.Should().HaveCount(1);
        context.Mensagens.Should().HaveCount(1);
        context.Atendimentos.Single().FarmaciaId.Should().Be(farmacia.Id);
        context.Atendimentos.Single().CanalEnum.Should().Be(CanalEnum.Instagram);
    }
}
