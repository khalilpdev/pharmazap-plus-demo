namespace PharmazapPlus.Web.Services;

public class QrCodeGenerateRequest
{
    public string InstanceName { get; set; } = string.Empty;
    public string ApiUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public bool ForceRecreate { get; set; }
}
