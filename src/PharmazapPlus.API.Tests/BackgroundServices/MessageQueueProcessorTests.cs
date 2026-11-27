using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using PharmazapPlus.API.BackgroundServices;
using PharmazapPlus.API.Hubs;
using PharmazapPlus.API.Services.Queue;
using PharmazapPlus.Shared;
using System.Reflection;
using System.Text.Json;

namespace PharmazapPlus.API.Tests.BackgroundServices;

public class MessageQueueProcessorTests
{
    [Theory]
    [InlineData("AtmendimentoId")]
    [InlineData("AtendimentoId")]
    public async Task ProcessQueuesAsync_WithJsonElementMessage_ProcessesSuccessfully(string atendimentoIdPropertyName)
    {
        // Arrange
        var messageQueueMock = new Mock<IMessageQueue>();
        var hubContextMock = new Mock<IHubContext<PharmaHub>>();
        var hubClientsMock = new Mock<IHubClients>();
        var clientProxyMock = new Mock<IClientProxy>();
        var loggerMock = new Mock<ILogger<MessageQueueProcessor>>();
        var configuration = new ConfigurationBuilder().Build();

        var queuePayload = JsonSerializer.Deserialize<object>($"{{\"FarmaciaId\":\"1\",\"{atendimentoIdPropertyName}\":123,\"IsNewAtendimento\":true}}")!;

        messageQueueMock.Setup(x => x.GetQueueLengthAsync(Consts.Filas.NovasMensagens)).ReturnsAsync(1);
        messageQueueMock.Setup(x => x.GetQueueLengthAsync(Consts.Filas.Notificacoes)).ReturnsAsync(0);
        messageQueueMock.Setup(x => x.GetQueueLengthAsync(Consts.Filas.Agendamentos)).ReturnsAsync(0);
        messageQueueMock.Setup(x => x.DequeueAsync<object>(Consts.Filas.NovasMensagens)).ReturnsAsync(queuePayload);

        clientProxyMock
            .Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        hubClientsMock.Setup(x => x.Group(It.IsAny<string>())).Returns(clientProxyMock.Object);
        hubContextMock.Setup(x => x.Clients).Returns(hubClientsMock.Object);

        var serviceProvider = new ServiceCollection()
            .AddScoped(_ => messageQueueMock.Object)
            .AddScoped(_ => hubContextMock.Object)
            .BuildServiceProvider();

        var processor = new MessageQueueProcessor(serviceProvider, loggerMock.Object, configuration);
        var processQueuesMethod = typeof(MessageQueueProcessor).GetMethod("ProcessQueuesAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        // Act
        var act = async () => await (Task)processQueuesMethod.Invoke(processor, null)!;

        // Assert
        await act.Should().NotThrowAsync();
        clientProxyMock.Verify(
            x => x.SendCoreAsync(Consts.SignalR.Metodos.NovaMensagemRecebida, It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
