using System.Text.Json.Serialization;
using PharmazapPlus.Shared.Models.CRM;

namespace PharmazapPlus.Web.Services;

internal class FarmaciaListResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public List<FarmaciaDto>? Data { get; set; }
}
