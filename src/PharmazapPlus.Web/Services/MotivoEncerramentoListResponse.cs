using System.Text.Json.Serialization;
using PharmazapPlus.Shared.Models.Mensagens;

namespace PharmazapPlus.Web.Services;

internal class MotivoEncerramentoListResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public List<MotivoEncerramentoDto>? Data { get; set; }
}
