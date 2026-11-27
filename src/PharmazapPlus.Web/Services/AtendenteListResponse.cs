using System.Text.Json.Serialization;

namespace PharmazapPlus.Web.Services;

internal class AtendenteListResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public List<AtendenteDto>? Data { get; set; }
}
