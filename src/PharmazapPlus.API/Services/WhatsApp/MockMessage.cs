namespace PharmazapPlus.API.Services.WhatsApp;

/// <summary>
/// Helper class to store mock messages for debugging.
/// </summary>
public class MockMessage
{
    public string Id { get; set; } = null!;
    public string FarmaciaId { get; set; } = null!;
    public string To { get; set; } = null!;
    public string Text { get; set; } = null!;
    public string MediaUrl { get; set; } = null!;
    public string MediaType { get; set; } = null!;
    public string? Caption { get; set; }
    public string TemplateName { get; set; } = null!;
    public string Type { get; set; } = null!;
    public DateTime Timestamp { get; set; }
}
