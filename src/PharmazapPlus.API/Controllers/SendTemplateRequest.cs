namespace PharmazapPlus.API.Controllers;

public record SendTemplateRequest
{
    public string To { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = "pt_BR";
    public Dictionary<string, object>? Parameters { get; set; }
}
