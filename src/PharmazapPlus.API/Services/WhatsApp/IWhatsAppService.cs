// PharmazapPlus.API/Services/WhatsApp/IWhatsAppService.cs
using PharmazapPlus.Shared.Models.Mensagens;

namespace PharmazapPlus.API.Services.WhatsApp;

public interface IWhatsAppService
{
    Task<bool> ValidateWebhook(string mode, string token, string challenge);
    Task<WhatsAppMessageResponse> SendTextMessage(string farmaciaId, string to, string text);
    Task<WhatsAppMessageResponse> SendMediaMessage(string farmaciaId, string to, string mediaUrl, string mediaType, string? caption = null);
    Task<WhatsAppMessageResponse> SendTemplateMessage(string farmaciaId, string to, string templateName, string languageCode, Dictionary<string, object>? parameters = null);
    Task<WebhookProcessingResult> ProcessWebhookPayload(Dictionary<string, object> payload);
    Task<string> SetupWebhook(string farmaciaId, string webhookUrl);
    Task<Dictionary<string, object>> GetBusinessProfile(string farmaciaId);
    Task<WhatsAppMessageResponse> SendInteractiveMessage(string farmaciaId, string to, string headerText, string bodyText, List<WhatsAppButton> buttons);
    Task<MessageStatus> GetMessageStatus(string farmaciaId, string messageId);
    Task<Dictionary<string, object>> GetBusinessMetrics(string farmaciaId, DateTime startDate, DateTime endDate);
}
