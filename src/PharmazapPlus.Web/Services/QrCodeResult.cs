namespace PharmazapPlus.Web.Services;

public class QrCodeResult
{
    public bool Success { get; set; }
    public string? Base64 { get; set; }
    public string? ImageUrl { get; set; }
    public bool AlreadyConnected { get; set; }
    public string? Error { get; set; }
    public string? TraceId { get; set; }
}
