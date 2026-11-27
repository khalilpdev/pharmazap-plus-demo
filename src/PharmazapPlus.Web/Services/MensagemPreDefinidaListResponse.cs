using System.Text.Json.Serialization;

namespace PharmazapPlus.Web.Services;

internal class MensagemPreDefinidaListResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public List<MensagemPreDefinidaDto>? Data { get; set; }
}
