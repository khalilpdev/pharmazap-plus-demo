using System.Text.Json.Serialization;

namespace PharmazapPlus.Web.Services;

internal class ApiSimpleResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
