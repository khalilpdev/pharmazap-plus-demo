namespace PharmazapPlus.Shared.Models.Config;

public class InstagramConfig
{
    /// <summary>
    /// Legacy flag kept for configuration compatibility.
    /// Runtime behavior is driven by the app environment and the official webhook flow.
    /// </summary>
    [Obsolete("Use IHostEnvironment.IsDevelopment() to determine runtime behavior. This property will be removed in a future version.")]
    public bool UseRealApi { get; set; } = false;

    /// <summary>
    /// Instagram Business Account ID (IGBA) linked to the Facebook Page.
    /// Required when UseRealApi = true.
    /// </summary>
    public string InstagramAccountId { get; set; } = "";

    /// <summary>
    /// Facebook Page ID linked to the Instagram Business Account.
    /// Required for some API endpoints.
    /// </summary>
    public string PageId { get; set; } = "";

    /// <summary>
    /// Meta Access Token with instagram_manage_messages permission.
    /// Required when UseRealApi = true.
    /// </summary>
    public string AccessToken { get; set; } = "";

    public string ApiVersion { get; set; } = "v21.0";
    public string ApiBaseUrl { get; set; } = "https://graph.instagram.com";

    /// <summary>
    /// Token used to verify webhook requests from Meta for Instagram.
    /// This is the token you configure in your Meta App's webhook settings.
    /// </summary>
    public string WebhookVerifyToken { get; set; } = "pharmazap_verify_token_2024";

    public bool DevelopmentMode { get; set; } = true;
    public bool MockMessages { get; set; } = true;
}
