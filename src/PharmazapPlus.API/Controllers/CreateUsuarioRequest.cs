using PharmazapPlus.Shared.Models.Core;

namespace PharmazapPlus.API.Controllers;

public record CreateUsuarioRequest
{
    public string Nome { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string? Telefone { get; set; }
    public string Role { get; set; } = UserRoles.Atendente;
    public int? FarmaciaId { get; set; }
    public string? Turno { get; set; }
    public bool? RecebeNotificacoes { get; set; }
}
