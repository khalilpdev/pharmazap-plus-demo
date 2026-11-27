using System.Text.Json.Serialization;
using PharmazapPlus.Shared.Models.Campanhas;

namespace PharmazapPlus.Web.Services;

internal class CampanhaListResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public List<Campanha>? Data { get; set; }
}
