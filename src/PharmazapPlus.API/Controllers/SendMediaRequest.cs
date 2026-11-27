using Microsoft.AspNetCore.Http;

namespace PharmazapPlus.API.Controllers;

public record SendMediaRequest
{
    public string To { get; set; } = string.Empty;
    public IFormFile MediaFile { get; set; } = null!;
    public string MediaType { get; set; } = string.Empty;
    public string Caption { get; set; } = string.Empty;
}
