using System.Text.Json.Serialization;
using PharmazapPlus.Shared.Models.Mensagens;

namespace PharmazapPlus.Web.Services;

internal class AtendimentoListResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("data")]
    public List<AtendimentoDto>? Data { get; set; }
}
