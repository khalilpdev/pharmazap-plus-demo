using System.Text.Json.Serialization;
using PharmazapPlus.Shared.Models.Mensagens;

namespace PharmazapPlus.Web.Services;

internal class EtiquetaListResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public List<EtiquetaDto>? Data { get; set; }
}
