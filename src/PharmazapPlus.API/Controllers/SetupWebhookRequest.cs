namespace PharmazapPlus.API.Controllers;

public record SetupWebhookRequest
{
    public string WebhookUrl { get; set; } = string.Empty;
}
