using System.Text.Json.Serialization;

namespace PharmazapPlus.Web.Services;

internal class AtendimentoClienteListResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public List<AtendimentoCompraDto>? Data { get; set; }
}
