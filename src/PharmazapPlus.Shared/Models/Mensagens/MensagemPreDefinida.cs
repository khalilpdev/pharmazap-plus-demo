using System.ComponentModel.DataAnnotations;
using PharmazapPlus.Shared.Models.Base;
using PharmazapPlus.Shared.Models.Core;

namespace PharmazapPlus.Shared.Models.Mensagens;

/// <summary>
/// Mensagem pré-definida cadastrada pela farmácia.
/// Permite que atendentes enviem mensagens padronizadas com um clique.
/// </summary>
public class MensagemPreDefinida : BaseEntity
{
    public int Id { get; set; }

    public int FarmaciaId { get; set; }
    public virtual Farmacia? Farmacia { get; set; }

    [Required]
    [MaxLength(200)]
    public string Titulo { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Texto { get; set; } = string.Empty;

    public bool Ativo { get; set; } = true;

    /// <summary>
    /// Ordem de exibição (menor = primeiro).
    /// </summary>
    public int Ordem { get; set; }
}
