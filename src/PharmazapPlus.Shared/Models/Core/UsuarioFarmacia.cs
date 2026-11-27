using PharmazapPlus.Shared.Models.Mensagens;
using System.ComponentModel.DataAnnotations;

namespace PharmazapPlus.Shared.Models.Core;

public class UsuarioFarmacia
{
    public int Id { get; set; }
    public int FarmaciaId { get; set; }
    public required Farmacia Farmacia { get; set; }

    [Required]
    [MaxLength(200)]
    public required string Nome { get; set; }

    [Required]
    [MaxLength(200)]
    public required string Email { get; set; }

    [MaxLength(20)]
    public required string Telefone { get; set; }

    public int? DepartamentoId { get; set; }

    /// <summary>CPF armazenado de forma criptografada (AES-256) em conformidade com a LGPD.</summary>
    [MaxLength(500)]
    public string? CPFEncrypted { get; set; }

    [Required]
    [MaxLength(50)]
    public string Role { get; set; } = UserRoles.Atendente;

    // Autenticação (em produção, usar Azure AD B2C)
    [MaxLength(500)]
    public required string PasswordHash { get; set; }

    [MaxLength(100)]
    public required string PasswordSalt { get; set; }

    // Status
    public bool Ativo { get; set; } = true;
    public bool EmailConfirmado { get; set; } = false;

    // Datas
    public DateTime DataCadastro { get; set; } = DateTime.UtcNow;
    public DateTime? UltimoLogin { get; set; }
    public DateTime? DataInativacao { get; set; }

    // Configurações
    [MaxLength(50)]
    public required string Turno { get; set; } // Manhã, Tarde, Noite

    public bool RecebeNotificacoes { get; set; } = true;

    [MaxLength(20)]
    public string Theme { get; set; } = "Light";

    [MaxLength(20)]
    public string? AccentColor { get; set; }

    // Estatísticas
    public int TotalAtendimentos { get; set; } = 0;
    public int AtendimentosHoje { get; set; } = 0;

    // Relacionamentos
    public virtual Departamento? Departamento { get; set; }
    public virtual ICollection<Atendimento> Atendimentos { get; set; } = new List<Atendimento>();

    // Propriedades calculadas
    public string NomeRole => UserRoles.GetRoleDisplayName(Role);

    public bool IsOnline => UltimoLogin.HasValue &&
                           (DateTime.UtcNow - UltimoLogin.Value).TotalMinutes < 5;
}
