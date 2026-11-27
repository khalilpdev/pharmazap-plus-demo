using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmazapPlus.Shared.Enums;
using PharmazapPlus.Shared.Models.Base;

namespace PharmazapPlus.Shared.Models.Mensagens;

/// <summary>
/// Representa um remetente bloqueado (lista negra de spam).
/// Pode ser bloqueado em escopo Local (por farmácia) ou Global (todas as farmácias).
/// </summary>
public class RemetenteBloqueado : BaseEntity
{
    public int Id { get; set; }

    /// <summary>
    /// Número de telefone normalizado (apenas dígitos, sem máscara, sem prefixo "55").
    /// Ex: "12998803214" para +55 (12) 99880-3214
    /// </summary>
    [Required]
    [MaxLength(11)]
    public required string NumeroNormalizado { get; set; }

    /// <summary>
    /// ID da farmácia para bloqueios locais. NULL indica bloqueio global.
    /// </summary>
    public int? FarmaciaId { get; set; }

    /// <summary>
    /// FK para Farmacia (apenas para bloqueios locais).
    /// </summary>
    public virtual Farmacia? Farmacia { get; set; }

    /// <summary>
    /// Escopo do bloqueio: Local (apenas para FarmaciaId) ou Global (todas as farmácias).
    /// </summary>
    public RemetentoBloqueioEscopoEnum Escopo { get; set; } = RemetentoBloqueioEscopoEnum.Local;

    /// <summary>
    /// Motivo do bloqueio (ex: "Spam", "Assédio", etc.).
    /// </summary>
    [MaxLength(200)]
    public string? MotivoBloqueia { get; set; }

    /// <summary>
    /// Data/hora quando o bloqueio foi criado.
    /// </summary>
    public DateTime DataBloqueio { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// ID do usuário que bloqueou o remetente.
    /// </summary>
    public int BloqueadoPorId { get; set; }

    /// <summary>
    /// Navegação para o usuário que bloqueou.
    /// </summary>
    public virtual UsuarioFarmacia? BloqueadoPor { get; set; }

    /// <summary>
    /// Snapshot do nome do usuário que bloqueou (preserva história se usuário for deletado).
    /// </summary>
    [MaxLength(150)]
    public required string NomeBloqueadoPor { get; set; }

    /// <summary>
    /// Data/hora quando foi desbloqueado (NULL se ainda ativo).
    /// </summary>
    public DateTime? DataDesbloqueio { get; set; }

    /// <summary>
    /// ID do usuário que desbloqueou o remetente (NULL se ainda bloqueado).
    /// </summary>
    public int? DesbloqueadoPorId { get; set; }

    /// <summary>
    /// Navegação para o usuário que desbloqueou.
    /// </summary>
    public virtual UsuarioFarmacia? DesbloqueadoPor { get; set; }

    /// <summary>
    /// Snapshot do nome do usuário que desbloqueou.
    /// </summary>
    [MaxLength(150)]
    public string? NomeDesbloqueadoPor { get; set; }

    /// <summary>
    /// Indica se o bloqueio está ativo (não foi desbloqueado).
    /// </summary>
    public bool Ativo { get; set; } = true;

    /// <summary>
    /// Propriedade derivada para verificar se o bloqueio é local.
    /// </summary>
    [NotMapped]
    public bool EhLocal => Escopo == RemetentoBloqueioEscopoEnum.Local && FarmaciaId.HasValue;

    /// <summary>
    /// Propriedade derivada para verificar se o bloqueio é global.
    /// </summary>
    [NotMapped]
    public bool EhGlobal => Escopo == RemetentoBloqueioEscopoEnum.Global && !FarmaciaId.HasValue;
}
