using System.Text.Json.Serialization;

namespace PharmazapPlus.Web.Services;

internal class ComprovanteUploadResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("comprovanteUrl")]
    public string? ComprovanteUrl { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
