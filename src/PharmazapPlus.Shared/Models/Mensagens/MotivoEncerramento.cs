using System.ComponentModel.DataAnnotations;
using PharmazapPlus.Shared.Models.Base;
using PharmazapPlus.Shared.Models.Core;

namespace PharmazapPlus.Shared.Models.Mensagens;

/// <summary>
/// Motivo de encerramento cadastrado pela farmácia.
/// Permite configurar se exige valor da venda e/ou comprovante (imagem).
/// </summary>
public class MotivoEncerramento : BaseEntity
{
    public int Id { get; set; }

    public int FarmaciaId { get; set; }
    public virtual Farmacia? Farmacia { get; set; }

    [Required]
    [MaxLength(200)]
    public string Nome { get; set; } = string.Empty;

    /// <summary>
    /// Quando true, o atendente deve informar o valor da venda ao finalizar com este motivo.
    /// </summary>
    public bool ExigeValorVenda { get; set; }

    /// <summary>
    /// Quando true, o atendente deve anexar um comprovante (imagem) ao finalizar com este motivo.
    /// Ex: foto do pedido, cupom fiscal, etc.
    /// </summary>
    public bool ExigeComprovante { get; set; }

    public bool Ativo { get; set; } = true;

    /// <summary>
    /// Ordem de exibição no dropdown de encerramento (menor = primeiro).
    /// </summary>
    public int Ordem { get; set; }
}
