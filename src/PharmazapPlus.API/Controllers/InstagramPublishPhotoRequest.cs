using Microsoft.AspNetCore.Http;

namespace PharmazapPlus.API.Controllers;

public record InstagramPublishPhotoRequest
{
    public IFormFile? ImageFile { get; init; }
    public string? Caption { get; init; }
}
