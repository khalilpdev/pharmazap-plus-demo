namespace PharmazapPlus.Web.Services;

public class EnsureInstanceResult
{
    public bool Success { get; set; }
    public bool Created { get; set; }
    public bool AlreadyExists { get; set; }
    public string? Error { get; set; }
    public string? TraceId { get; set; }
}
