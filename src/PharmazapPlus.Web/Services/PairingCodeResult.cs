namespace PharmazapPlus.Web.Services;

public class PairingCodeResult
{
    public bool Success { get; set; }
    public string? Code { get; set; }
    public string? Error { get; set; }
    public string? TraceId { get; set; }
}
