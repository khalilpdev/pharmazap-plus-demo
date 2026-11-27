namespace PharmazapPlus.Web.Services;

public class ListInstancesResult
{
    public bool Success { get; set; }
    public List<InstanceOption> Instances { get; set; } = new();
    public string? Error { get; set; }
}
