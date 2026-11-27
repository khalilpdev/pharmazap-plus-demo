#nullable disable

using FluentAssertions;
#pragma warning disable CS0618 // UseRealApi is obsolete
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using PharmazapPlus.API.Services.WhatsApp;
using PharmazapPlus.Shared.Models.Config;
using PharmazapPlus.Shared.Models.Mensagens;

namespace PharmazapPlus.API.Tests.Services;

/// <summary>
/// Unit tests for MockWhatsAppService to ensure it properly simulates WhatsApp functionality
/// without making real API calls.
/// </summary>
public class MockWhatsAppServiceTests
{
    private readonly Mock<ILogger<MockWhatsAppService>> _mockLogger;
    private readonly WhatsAppConfig _config;
    private readonly MockWhatsAppService _service;

    public MockWhatsAppServiceTests()
    {
        _mockLogger = new Mock<ILogger<MockWhatsAppService>>();
        _config = new WhatsAppConfig
        {
            UseRealApi = false,
            PhoneNumberId = "mock-phone-id",
            AccessToken = "mock-access-token",
            DevelopmentMode = true,
            MockMessages = true
        };

        var options = Options.Create(_config);
        _service = new MockWhatsAppService(_mockLogger.Object, options);
    }

    [Fact]
    public async Task ValidateWebhook_AlwaysReturnsTrue()
    {
        // Arrange
        var mode = "subscribe";
        var token = "any-token";
        var challenge = "test-challenge";

        // Act
        var result = await _service.ValidateWebhook(mode, token, challenge);

        // Assert
        result.Should().BeTrue("Mock service should always validate webhooks successfully");
    }

    [Fact]
    public async Task SendTextMessage_ReturnsSuccessResponse()
    {
        // Arrange
        var farmaciaId = "1";
        var to = "+5562998808389";
        var text = "Olá! Sua receita está pronta";

        // Act
        var result = await _service.SendTextMessage(farmaciaId, to, text);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue("Mock service should return success");
        result.MessageId.Should().NotBeNullOrEmpty("MessageId should be generated");
        result.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.Metadata.Should().ContainKey("mock");
        result.Metadata["mock"].Should().Be(true);
        result.Metadata.Should().ContainKey("farmaciaId");
        result.Metadata["farmaciaId"].Should().Be(farmaciaId);
    }

    [Fact]
    public async Task SendMediaMessage_ReturnsSuccessResponse()
    {
        // Arrange
        var farmaciaId = "1";
        var to = "+5562998808389";
        var mediaUrl = "https://example.com/image.jpg";
        var mediaType = "image";
        var caption = "Imagem de exemplo";

        // Act
        var result = await _service.SendMediaMessage(farmaciaId, to, mediaUrl, mediaType, caption);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue("Mock service should return success");
        result.MessageId.Should().NotBeNullOrEmpty("MessageId should be generated");
        result.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.Metadata.Should().ContainKey("mock");
        result.Metadata.Should().ContainKey("mediaType");
        result.Metadata["mediaType"].Should().Be(mediaType);
    }

    [Fact]
    public async Task SendTemplateMessage_ReturnsSuccessResponse()
    {
        // Arrange
        var farmaciaId = "1";
        var to = "+5562998808389";
        var templateName = "welcome";
        var languageCode = "pt_BR";
        var parameters = new Dictionary<string, object>
        {
            { "name", "João" },
            { "pharmacy", "Farmácia Central" }
        };

        // Act
        var result = await _service.SendTemplateMessage(farmaciaId, to, templateName, languageCode, parameters);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue("Mock service should return success");
        result.MessageId.Should().NotBeNullOrEmpty("MessageId should be generated");
        result.Metadata.Should().ContainKey("templateName");
        result.Metadata["templateName"].Should().Be(templateName);
    }

    [Fact]
    public async Task ProcessWebhookPayload_ReturnsSuccessResult()
    {
        // Arrange
        var payload = new Dictionary<string, object>
        {
            { "object", "whatsapp_business_account" },
            { "entry", new[] { new { id = "123" } } }
        };

        // Act
        var result = await _service.ProcessWebhookPayload(payload);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue("Mock service should process webhooks successfully");
        result.Message.Should().Contain("mock");
        result.NewAtendimentoCreated.Should().BeFalse("Mock doesn't create real records");
        result.NewClientCreated.Should().BeFalse("Mock doesn't create real records");
    }

    [Fact]
    public async Task SetupWebhook_ReturnsWebhookUrl()
    {
        // Arrange
        var farmaciaId = "1";
        var webhookUrl = "https://example.com/webhook";

        // Act
        var result = await _service.SetupWebhook(farmaciaId, webhookUrl);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Be(webhookUrl, "Mock should return the same webhook URL");
    }

    [Fact]
    public async Task GetBusinessProfile_ReturnsMockProfile()
    {
        // Arrange
        var farmaciaId = "1";

        // Act
        var result = await _service.GetBusinessProfile(farmaciaId);

        // Assert
        result.Should().NotBeNull();
        result.Should().ContainKey("id");
        result.Should().ContainKey("name");
        result.Should().ContainKey("phone");
        result.Should().ContainKey("mock");
        result["mock"].Should().Be(true);
        result["phone"].Should().Be("+55 62998808389", "Should use test phone number");
    }

    [Fact]
    public async Task SendInteractiveMessage_ReturnsSuccessResponse()
    {
        // Arrange
        var farmaciaId = "1";
        var to = "+5562998808389";
        var headerText = "Escolha uma opção";
        var bodyText = "Como podemos ajudar você?";
        var buttons = new List<WhatsAppButton>
        {
            new WhatsAppButton { Type = "reply", Title = "Consultar horário", Payload = "consultar_horario" },
            new WhatsAppButton { Type = "reply", Title = "Falar com atendente", Payload = "atendente" }
        };

        // Act
        var result = await _service.SendInteractiveMessage(farmaciaId, to, headerText, bodyText, buttons);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue("Mock service should return success");
        result.MessageId.Should().NotBeNullOrEmpty("MessageId should be generated");
        result.Metadata.Should().ContainKey("buttonCount");
        result.Metadata["buttonCount"].Should().Be(2);
    }

    [Fact]
    public async Task GetMessageStatus_ReturnsDeliveredStatus()
    {
        // Arrange
        var farmaciaId = "1";
        var messageId = Guid.NewGuid().ToString();

        // Act
        var result = await _service.GetMessageStatus(farmaciaId, messageId);

        // Assert
        result.Should().Be(MessageStatus.Entregue, "Mock service should always return Delivered status");
    }

    [Fact]
    public async Task GetBusinessMetrics_ReturnsMockMetrics()
    {
        // Arrange
        var farmaciaId = "1";
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;

        // Act
        var result = await _service.GetBusinessMetrics(farmaciaId, startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.Should().ContainKey("farmaciaId");
        result.Should().ContainKey("messagesSent");
        result.Should().ContainKey("messagesReceived");
        result.Should().ContainKey("mock");
        result["mock"].Should().Be(true);
        result["messagesSent"].Should().BeOfType<int>();
        ((int)result["messagesSent"]).Should().BeGreaterThan(0, "Mock metrics should have realistic values");
    }

    [Fact]
    public async Task SendTextMessage_StoresMessageInInternalList()
    {
        // Arrange
        var farmaciaId = "1";
        var to = "+5562998808389";
        var text = "Test message";

        // Act
        var result = await _service.SendTextMessage(farmaciaId, to, text);
        var sentMessages = _service.GetSentMessages();

        // Assert
        sentMessages.Should().NotBeEmpty("Message should be stored");
        var lastMessage = sentMessages.Last();
        lastMessage.Id.Should().Be(result.MessageId);
        lastMessage.FarmaciaId.Should().Be(farmaciaId);
        lastMessage.To.Should().Be(to);
        lastMessage.Text.Should().Be(text);
        lastMessage.Type.Should().Be("text");
    }

    [Fact]
    public async Task SendMediaMessage_StoresMessageInInternalList()
    {
        // Arrange
        var farmaciaId = "1";
        var to = "+5562998808389";
        var mediaUrl = "https://example.com/image.jpg";
        var mediaType = "image";

        // Act
        var result = await _service.SendMediaMessage(farmaciaId, to, mediaUrl, mediaType);
        var sentMessages = _service.GetSentMessages();

        // Assert
        sentMessages.Should().NotBeEmpty("Message should be stored");
        var lastMessage = sentMessages.Last();
        lastMessage.Id.Should().Be(result.MessageId);
        lastMessage.MediaUrl.Should().Be(mediaUrl);
        lastMessage.MediaType.Should().Be(mediaType);
        lastMessage.Type.Should().Be("media");
    }

    [Fact]
    public async Task MultipleSendTextMessage_StoresAllMessages()
    {
        // Arrange
        var farmaciaId = "1";
        var to = "+5562998808389";

        // Act
        await _service.SendTextMessage(farmaciaId, to, "Message 1");
        await _service.SendTextMessage(farmaciaId, to, "Message 2");
        await _service.SendTextMessage(farmaciaId, to, "Message 3");

        var sentMessages = _service.GetSentMessages();

        // Assert
        sentMessages.Should().HaveCount(3, "All messages should be stored");
        sentMessages.Select(m => m.Text).Should().Contain(new[] { "Message 1", "Message 2", "Message 3" });
    }

    [Fact]
    public void Constructor_LogsInitializationMessage()
    {
        // Arrange & Act
        var logger = new Mock<ILogger<MockWhatsAppService>>();
        var options = Options.Create(_config);
        var service = new MockWhatsAppService(logger.Object, options);

        // Assert
        // Verify that initialization logging occurred
        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("MOCK WhatsApp Service")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task AllMethods_NeverThrowException()
    {
        // Arrange
        var farmaciaId = "1";
        var to = "+5562998808389";

        // Act & Assert - None of these should throw exceptions
        await FluentActions.Invoking(async () => await _service.ValidateWebhook("subscribe", "token", "challenge"))
            .Should().NotThrowAsync("ValidateWebhook should not throw");

        await FluentActions.Invoking(async () => await _service.SendTextMessage(farmaciaId, to, "text"))
            .Should().NotThrowAsync("SendTextMessage should not throw");

        await FluentActions.Invoking(async () => await _service.SendMediaMessage(farmaciaId, to, "url", "image"))
            .Should().NotThrowAsync("SendMediaMessage should not throw");

        await FluentActions.Invoking(async () => await _service.SendTemplateMessage(farmaciaId, to, "template", "pt_BR"))
            .Should().NotThrowAsync("SendTemplateMessage should not throw");

        await FluentActions.Invoking(async () => await _service.ProcessWebhookPayload(new Dictionary<string, object>()))
            .Should().NotThrowAsync("ProcessWebhookPayload should not throw");

        await FluentActions.Invoking(async () => await _service.SetupWebhook(farmaciaId, "url"))
            .Should().NotThrowAsync("SetupWebhook should not throw");

        await FluentActions.Invoking(async () => await _service.GetBusinessProfile(farmaciaId))
            .Should().NotThrowAsync("GetBusinessProfile should not throw");

        await FluentActions.Invoking(async () => await _service.SendInteractiveMessage(farmaciaId, to, "header", "body", new List<WhatsAppButton>()))
            .Should().NotThrowAsync("SendInteractiveMessage should not throw");

        await FluentActions.Invoking(async () => await _service.GetMessageStatus(farmaciaId, "msgId"))
            .Should().NotThrowAsync("GetMessageStatus should not throw");

        await FluentActions.Invoking(async () => await _service.GetBusinessMetrics(farmaciaId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow))
            .Should().NotThrowAsync("GetBusinessMetrics should not throw");
    }
}
#nullable restore
