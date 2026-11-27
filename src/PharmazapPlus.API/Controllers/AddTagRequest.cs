namespace PharmazapPlus.API.Controllers;

public record AddTagRequest
{
    public string Tag { get; set; } = string.Empty;
    public string Cor { get; set; } = string.Empty;
}
