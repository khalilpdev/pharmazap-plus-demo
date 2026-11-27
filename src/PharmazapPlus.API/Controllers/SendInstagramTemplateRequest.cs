namespace PharmazapPlus.API.Controllers;

public record SendInstagramTemplateRequest
{
    public string To { get; set; } = "";
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string? ImageUrl { get; set; }
    public string? DefaultActionUrl { get; set; }
    public List<InstagramTemplateButton>? Buttons { get; set; }
}

public record InstagramTemplateButton
{
    public string Type { get; set; } = "postback"; // "postback", "web_url"
    public string Title { get; set; } = "";
    public string Payload { get; set; } = "";
    public string? Url { get; set; }
}
