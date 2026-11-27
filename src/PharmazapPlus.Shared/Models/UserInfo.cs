using System.Text.Json.Serialization;

namespace PharmazapPlus.Shared.Models;

public class UserInfo
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("email")]
    public required string Email { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("role")]
    public required string Role { get; set; }

    [JsonPropertyName("farmaciaId")]
    public required string FarmaciaId { get; set; }

    [JsonPropertyName("farmaciaNome")]
    public required string FarmaciaNome { get; set; }

    [JsonPropertyName("farmaciaLogoUrl")]
    public string? FarmaciaLogoUrl { get; set; }

    [JsonPropertyName("token")]
    public required string Token { get; set; }

    [JsonPropertyName("expiration")]
    public DateTime Expiration { get; set; }

    [JsonPropertyName("avatarUrl")]
    public required string AvatarUrl { get; set; }

    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "Light";

    [JsonPropertyName("accentColor")]
    public string? AccentColor { get; set; }

    [JsonPropertyName("termoAceiteRequired")]
    public bool TermoAceiteRequired { get; set; }

    // Propriedades calculadas
    [JsonIgnore]
    public bool IsGestor => UserRoles.IsGestorLike(Role);

    [JsonIgnore]
    public bool IsAdministrador => Role == UserRoles.Administrador;

    [JsonIgnore]
    public bool IsAtendente => Role == UserRoles.Atendente;

    [JsonIgnore]
    public string RoleDisplayName => UserRoles.GetRoleDisplayName(Role);

    [JsonIgnore]
    public bool HasFarmacia => !string.IsNullOrEmpty(FarmaciaId) && FarmaciaId != "0";

    public bool IsAuthenticated { get; set; }
}
