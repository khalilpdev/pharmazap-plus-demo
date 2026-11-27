using PharmazapPlus.Shared.Models.Mensagens;

namespace PharmazapPlus.API.Services.Instagram;

public interface IInstagramService
{
    Task<bool> ValidateWebhook(string mode, string token, string challenge);
    Task<InstagramMessageResponse> SendTextMessage(string farmaciaId, string recipientId, string text);
    Task<InstagramMessageResponse> SendMediaMessage(string farmaciaId, string recipientId, string mediaUrl, string mediaType, string? caption = null);
    Task<InstagramMessageResponse> SendTemplateMessage(string farmaciaId, string recipientId, string templateTitle, string templateSubtitle, List<InstagramButton>? buttons = null, string? imageUrl = null, string? defaultActionUrl = null);
    Task<WebhookProcessingResult> ProcessWebhookPayload(Dictionary<string, object> payload);
    Task<string> SetupWebhook(string farmaciaId, string webhookUrl);
    Task<Dictionary<string, object>> GetBusinessProfile(string farmaciaId);
    Task<InstagramMessageResponse> SendInteractiveMessage(string farmaciaId, string recipientId, string headerText, string bodyText, List<InstagramButton> buttons);
    Task<MessageStatus> GetMessageStatus(string farmaciaId, string messageId);
}
