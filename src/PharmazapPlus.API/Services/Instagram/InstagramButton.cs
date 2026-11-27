namespace PharmazapPlus.API.Services.Instagram;

public class InstagramButton
{
    public string Type { get; set; } = "postback"; // "postback", "web_url"
    public string Title { get; set; } = "";
    public string Payload { get; set; } = "";
    public string? Url { get; set; }
}
