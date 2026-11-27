using System.Text.Json.Serialization;
using PharmazapPlus.Shared.Models.Core;

namespace PharmazapPlus.Web.Services
{
    public record CreateAdministradorDto
    {
        public string Nome { get; set; } = "";
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string? Telefone { get; set; }
        public string? Turno { get; set; }
        public bool RecebeNotificacoes { get; set; } = true;

        [JsonPropertyName("role")]
        public string Role { get; set; } = UserRoles.Administrador;
    }
}
