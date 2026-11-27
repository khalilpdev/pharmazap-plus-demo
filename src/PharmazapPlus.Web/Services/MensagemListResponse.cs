using System.Text.Json.Serialization;

namespace PharmazapPlus.Web.Services;

internal class MensagemListResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public List<MensagemApiDto>? Data { get; set; }
}
