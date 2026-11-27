namespace PharmazapPlus.Web.Services;

public sealed class MetaOAuthStartResult
{
    public bool Success { get; set; }
    public string? AuthorizationUrl { get; set; }
    public string? State { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
}

public sealed class MetaOAuthPopupResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? AccessToken { get; set; }
    public string? PageId { get; set; }
    public string? PageName { get; set; }
    public string? InstagramBusinessId { get; set; }
    public string? WhatsAppBusinessAccountId { get; set; }
    public string? PhoneNumberId { get; set; }
    public string? BusinessAccountId { get; set; }
    public string? ErrorCode { get; set; }
}
