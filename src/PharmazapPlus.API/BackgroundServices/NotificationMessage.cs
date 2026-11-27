namespace PharmazapPlus.API.BackgroundServices;

public class NotificationMessage
{
    public string Type { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string FarmaciaId { get; set; } = null!;
    public Dictionary<string, object> Data { get; set; } = new();
}
