namespace PharmazapPlus.Shared.Models.Config;

public class WhatsAppConfig
{
    /// <summary>
    /// Controls whether to use real WhatsApp API or mock service.
    /// Set to false for development/testing (uses MockWhatsAppService).
    /// Set to true for production (uses real WhatsAppService with Meta API).
    /// </summary>
    [Obsolete("Use IHostEnvironment.IsDevelopment() to determine runtime behavior. This property will be removed in a future version.")]
    public bool UseRealApi { get; set; } = false;
    
    /// <summary>
    /// WhatsApp Business Phone Number ID from Meta.
    /// Required when UseRealApi = true.
    /// </summary>
    public string PhoneNumberId { get; set; } = "";
    
    /// <summary>
    /// Meta Access Token for WhatsApp Business API.
    /// Required when UseRealApi = true.
    /// </summary>
    public string AccessToken { get; set; } = "";
    
    public string ApiVersion { get; set; } = "v23.0";
    public string ApiBaseUrl { get; set; } = "https://graph.facebook.com";
    
    /// <summary>
    /// Token used to verify webhook requests from Meta.
    /// This is the token you configure in your Meta App's webhook settings.
    /// </summary>
    public string WebhookVerifyToken { get; set; } = "pharmazap_verify_token_2024";
    
    public bool DevelopmentMode { get; set; } = true;
    public bool MockMessages { get; set; } = true;
}
