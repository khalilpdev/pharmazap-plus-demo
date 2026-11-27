namespace PharmazapPlus.API.Services.WhatsApp;

public class WhatsAppButton
{
    public string Type { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Payload { get; set; } = null!;
    public string Url { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
}
