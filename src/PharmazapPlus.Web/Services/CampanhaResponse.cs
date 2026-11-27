using System.Text.Json.Serialization;
using PharmazapPlus.Shared.Models.Campanhas;

namespace PharmazapPlus.Web.Services;

internal class CampanhaResponse
{
    [JsonPropertyName("data")]
    public Campanha? Data { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
