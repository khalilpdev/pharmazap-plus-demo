using System.Text.Json.Serialization;

namespace PharmazapPlus.Web.Services;

public sealed class FarmaciaLogoUploadResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("logoUrl")]
    public string? LogoUrl { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
