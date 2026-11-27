using System.Text.Json.Serialization;

namespace PharmazapPlus.Web.Services;

internal class DepartamentoListResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public List<DepartamentoDto>? Data { get; set; }
}
