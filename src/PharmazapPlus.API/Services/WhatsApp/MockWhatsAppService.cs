#nullable enable

using Microsoft.Extensions.Options;
using PharmazapPlus.Shared.Models.Config;
using PharmazapPlus.Shared.Models.Mensagens;
using System.Collections.Concurrent;

namespace PharmazapPlus.API.Services.WhatsApp;

/// <summary>
/// Mock implementation of WhatsApp service for development and testing.
/// This service simulates WhatsApp functionality without requiring real WhatsApp Business credentials.
/// 
/// Usage:
/// - Set "WhatsApp:UseRealApi" to false in appsettings to use this mock service
/// - All messages are logged to console but NOT sent to real WhatsApp
/// - Useful for development, testing, and demos without Meta/WhatsApp account
/// - For production, set "WhatsApp:UseRealApi" to true and configure real tokens
/// 
/// Example test phone number: +55 62998808389
/// </summary>
public class MockWhatsAppService : IWhatsAppService
{
    private readonly ILogger<MockWhatsAppService> _logger;
    private readonly WhatsAppConfig _config;
    private readonly ConcurrentBag<MockMessage> _sentMessages = new();

    public MockWhatsAppService(
        ILogger<MockWhatsAppService> logger,
        IOptions<WhatsAppConfig> config)
    {
        _logger = logger;
        _config = config.Value;

        _logger.LogInformation("🎭 MOCK WhatsApp Service initialized");
        _logger.LogInformation("⚠️  This is a MOCK service - no real messages will be sent!");
    }

    /// <summary>
    /// Validates webhook requests. Always returns true for mock service.
    /// </summary>
    public Task<bool> ValidateWebhook(string mode, string token, string challenge)
    {
        _logger.LogInformation("✅ MOCK Webhook: Validating webhook");
        _logger.LogInformation("   Mode: {Mode}", mode);
        _logger.LogInformation("   Token: {Token}", token);
        _logger.LogInformation("   Challenge: {Challenge}", challenge);
        _logger.LogInformation("   ✅ Validation successful (mock)");

        return Task.FromResult(true);
    }

    /// <summary>
    /// Sends a text message. Logs to console and returns mock success response.
    /// </summary>
    public Task<WhatsAppMessageResponse> SendTextMessage(string farmaciaId, string to, string text)
    {
        var messageId = Guid.NewGuid().ToString();
        var timestamp = DateTime.UtcNow;

        _logger.LogInformation("📱 MOCK WhatsApp: Sending text message");
        _logger.LogInformation("   Farmácia: {FarmaciaId}", farmaciaId);
        _logger.LogInformation("   Para: {To}", to);
        _logger.LogInformation("   Mensagem: {Text}", text);
        _logger.LogInformation("   MessageId: {MessageId}", messageId);
        _logger.LogInformation("   ✅ Success simulated");

        // Store message for debugging
        _sentMessages.Add(new MockMessage
        {
            Id = messageId,
            FarmaciaId = farmaciaId,
            To = to,
            Text = text,
            Type = "text",
            Timestamp = timestamp
        });

        return Task.FromResult(new WhatsAppMessageResponse
        {
            Success = true,
            MessageId = messageId,
            Timestamp = timestamp,
            Metadata = new Dictionary<string, object>
            {
                { "mock", true },
                { "farmaciaId", farmaciaId },
                { "to", to }
            }
        });
    }

    /// <summary>
    /// Sends a media message. Logs to console and returns mock success response.
    /// </summary>
    public Task<WhatsAppMessageResponse> SendMediaMessage(
        string farmaciaId,
        string to,
        string mediaUrl,
        string mediaType,
        string? caption = null)
    {
        var messageId = Guid.NewGuid().ToString();
        var timestamp = DateTime.UtcNow;

        _logger.LogInformation("📷 MOCK WhatsApp: Sending media message");
        _logger.LogInformation("   Farmácia: {FarmaciaId}", farmaciaId);
        _logger.LogInformation("   Para: {To}", to);
        _logger.LogInformation("   Media URL: {MediaUrl}", mediaUrl);
        _logger.LogInformation("   Media Type: {MediaType}", mediaType);
        _logger.LogInformation("   Caption: {Caption}", caption ?? "(none)");
        _logger.LogInformation("   MessageId: {MessageId}", messageId);
        _logger.LogInformation("   ✅ Success simulated");

        // Store message for debugging
        _sentMessages.Add(new MockMessage
        {
            Id = messageId,
            FarmaciaId = farmaciaId,
            To = to,
            MediaUrl = mediaUrl,
            MediaType = mediaType,
            Caption = caption,
            Type = "media",
            Timestamp = timestamp
        });

        return Task.FromResult(new WhatsAppMessageResponse
        {
            Success = true,
            MessageId = messageId,
            Timestamp = timestamp,
            Metadata = new Dictionary<string, object>
            {
                { "mock", true },
                { "farmaciaId", farmaciaId },
                { "mediaType", mediaType }
            }
        });
    }

    /// <summary>
    /// Sends a template message. Logs to console and returns mock success response.
    /// </summary>
    public Task<WhatsAppMessageResponse> SendTemplateMessage(
        string farmaciaId,
        string to,
        string templateName,
        string languageCode,
        Dictionary<string, object>? parameters = null)
    {
        var messageId = Guid.NewGuid().ToString();
        var timestamp = DateTime.UtcNow;

        _logger.LogInformation("📋 MOCK WhatsApp: Sending template message");
        _logger.LogInformation("   Farmácia: {FarmaciaId}", farmaciaId);
        _logger.LogInformation("   Para: {To}", to);
        _logger.LogInformation("   Template: {TemplateName}", templateName);
        _logger.LogInformation("   Language: {LanguageCode}", languageCode);
        if (parameters != null && parameters.Any())
        {
            _logger.LogInformation("   Parameters:");
            foreach (var param in parameters)
            {
                _logger.LogInformation("      {Key}: {Value}", param.Key, param.Value);
            }
        }
        _logger.LogInformation("   MessageId: {MessageId}", messageId);
        _logger.LogInformation("   ✅ Success simulated");

        // Store message for debugging
        _sentMessages.Add(new MockMessage
        {
            Id = messageId,
            FarmaciaId = farmaciaId,
            To = to,
            TemplateName = templateName,
            Type = "template",
            Timestamp = timestamp
        });

        return Task.FromResult(new WhatsAppMessageResponse
        {
            Success = true,
            MessageId = messageId,
            Timestamp = timestamp,
            Metadata = new Dictionary<string, object>
            {
                { "mock", true },
                { "farmaciaId", farmaciaId },
                { "templateName", templateName }
            }
        });
    }

    /// <summary>
    /// Processes incoming webhook payloads. Logs to console and returns mock success.
    /// </summary>
    public Task<WebhookProcessingResult> ProcessWebhookPayload(Dictionary<string, object> payload)
    {
        _logger.LogInformation("🔔 MOCK WhatsApp: Processing webhook payload");
        _logger.LogInformation("   Payload keys: {Keys}", string.Join(", ", payload.Keys));
        foreach (var item in payload.Take(5)) // Log first 5 items
        {
            _logger.LogInformation("   {Key}: {Value}", item.Key, item.Value);
        }
        _logger.LogInformation("   ✅ Processing successful (mock)");

        return Task.FromResult(new WebhookProcessingResult
        {
            Success = true,
            Message = "Webhook processed successfully (mock)",
            ProcessedMessage = null,
            Atendimento = null,
            NewAtendimentoCreated = false,
            NewClientCreated = false
        });
    }

    /// <summary>
    /// Sets up webhook configuration. Logs to console and returns mock URL.
    /// </summary>
    public Task<string> SetupWebhook(string farmaciaId, string webhookUrl)
    {
        _logger.LogInformation("🔧 MOCK WhatsApp: Setting up webhook");
        _logger.LogInformation("   Farmácia: {FarmaciaId}", farmaciaId);
        _logger.LogInformation("   Webhook URL: {WebhookUrl}", webhookUrl);
        _logger.LogInformation("   ✅ Setup successful (mock)");

        return Task.FromResult(webhookUrl);
    }

    /// <summary>
    /// Gets business profile. Returns mock profile data.
    /// </summary>
    public Task<Dictionary<string, object>> GetBusinessProfile(string farmaciaId)
    {
        _logger.LogInformation("🏢 MOCK WhatsApp: Getting business profile");
        _logger.LogInformation("   Farmácia: {FarmaciaId}", farmaciaId);

        var profile = new Dictionary<string, object>
        {
            { "id", farmaciaId },
            { "name", "Farmácia Mock - Development" },
            { "phone", "+55 62998808389" },
            { "description", "Mock pharmacy profile for testing" },
            { "address", "Rua Mock, 123 - Cidade de Desenvolvimento" },
            { "email", "contato@farmaciamock.com.br" },
            { "website", "https://farmaciamock.com.br" },
            { "category", "Healthcare - Pharmacy" },
            { "mock", true }
        };

        _logger.LogInformation("   ✅ Profile returned (mock)");

        return Task.FromResult(profile);
    }

    /// <summary>
    /// Sends interactive message with buttons. Logs to console and returns mock success.
    /// </summary>
    public Task<WhatsAppMessageResponse> SendInteractiveMessage(
        string farmaciaId,
        string to,
        string headerText,
        string bodyText,
        List<WhatsAppButton> buttons)
    {
        var messageId = Guid.NewGuid().ToString();
        var timestamp = DateTime.UtcNow;

        _logger.LogInformation("🎯 MOCK WhatsApp: Sending interactive message");
        _logger.LogInformation("   Farmácia: {FarmaciaId}", farmaciaId);
        _logger.LogInformation("   Para: {To}", to);
        _logger.LogInformation("   Header: {HeaderText}", headerText);
        _logger.LogInformation("   Body: {BodyText}", bodyText);
        _logger.LogInformation("   Buttons: {ButtonCount}", buttons?.Count ?? 0);
        if (buttons != null && buttons.Any())
        {
            foreach (var button in buttons)
            {
                _logger.LogInformation("      - {Type}: {Title}", button.Type, button.Title);
            }
        }
        _logger.LogInformation("   MessageId: {MessageId}", messageId);
        _logger.LogInformation("   ✅ Success simulated");

        // Store message for debugging
        _sentMessages.Add(new MockMessage
        {
            Id = messageId,
            FarmaciaId = farmaciaId,
            To = to,
            Text = $"[Interactive] {headerText}: {bodyText}",
            Type = "interactive",
            Timestamp = timestamp
        });

        return Task.FromResult(new WhatsAppMessageResponse
        {
            Success = true,
            MessageId = messageId,
            Timestamp = timestamp,
            Metadata = new Dictionary<string, object>
            {
                { "mock", true },
                { "farmaciaId", farmaciaId },
                { "buttonCount", buttons?.Count ?? 0 }
            }
        });
    }

    /// <summary>
    /// Gets message status. Returns mock "Delivered" status.
    /// </summary>
    public Task<MessageStatus> GetMessageStatus(string farmaciaId, string messageId)
    {
        _logger.LogInformation("📊 MOCK WhatsApp: Getting message status");
        _logger.LogInformation("   Farmácia: {FarmaciaId}", farmaciaId);
        _logger.LogInformation("   MessageId: {MessageId}", messageId);
        _logger.LogInformation("   Status: Delivered (mock)");
        _logger.LogInformation("   ✅ Status retrieved");

        return Task.FromResult(MessageStatus.Entregue);
    }

    /// <summary>
    /// Gets business metrics. Returns mock metrics data.
    /// </summary>
    public Task<Dictionary<string, object>> GetBusinessMetrics(
        string farmaciaId,
        DateTime startDate,
        DateTime endDate)
    {
        _logger.LogInformation("📈 MOCK WhatsApp: Getting business metrics");
        _logger.LogInformation("   Farmácia: {FarmaciaId}", farmaciaId);
        _logger.LogInformation("   Period: {StartDate} to {EndDate}", startDate, endDate);

        var metrics = new Dictionary<string, object>
        {
            { "farmaciaId", farmaciaId },
            { "startDate", startDate },
            { "endDate", endDate },
            { "messagesSent", 150 },
            { "messagesReceived", 89 },
            { "messagesDelivered", 145 },
            { "messagesRead", 120 },
            { "messagesFailed", 5 },
            { "conversationsStarted", 42 },
            { "averageResponseTime", "3.5 minutes" },
            { "mock", true }
        };

        _logger.LogInformation("   Messages Sent: {MessagesSent}", metrics["messagesSent"]);
        _logger.LogInformation("   Messages Received: {MessagesReceived}", metrics["messagesReceived"]);
        _logger.LogInformation("   ✅ Metrics returned (mock)");

        return Task.FromResult(metrics);
    }

    /// <summary>
    /// Gets all sent messages for debugging purposes.
    /// </summary>
    public List<MockMessage> GetSentMessages()
    {
        return _sentMessages.ToList();
    }
}
