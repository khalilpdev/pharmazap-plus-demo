using System.Text.Json.Serialization;

namespace PharmazapPlus.Web.Services;

internal class UsuariosListResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public List<AdministradorDto>? Data { get; set; }
}
