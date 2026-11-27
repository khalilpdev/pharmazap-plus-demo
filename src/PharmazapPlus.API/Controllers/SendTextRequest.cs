namespace PharmazapPlus.API.Controllers;

public record SendTextRequest
{
    public string To { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}
