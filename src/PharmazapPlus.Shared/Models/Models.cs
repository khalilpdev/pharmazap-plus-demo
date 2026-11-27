
namespace PharmazapPlus.Shared.Models;

/// <summary>
/// Modelo de Usuário
/// </summary>
public class Usuario
{
    public Guid Id { get; set; }
    public required string Nome { get; set; }
    public required string Email { get; set; }
    public required string Telefone { get; set; }
    public UserRole Role { get; set; }
    public bool Ativo { get; set; }
}
