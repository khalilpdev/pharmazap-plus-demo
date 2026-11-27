namespace PharmazapPlus.Web.Services;

public class InstanceInfoResult
{
    public bool Success { get; set; }
    public bool Exists { get; set; }
    public string? Status { get; set; }
    public string? Error { get; set; }
}
