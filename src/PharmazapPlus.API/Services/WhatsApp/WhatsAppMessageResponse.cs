namespace PharmazapPlus.API.Services.WhatsApp;

public record WhatsAppMessageResponse
{
    public bool Success { get; set; }
    public string? MessageId { get; set; }
    public DateTime Timestamp { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}
