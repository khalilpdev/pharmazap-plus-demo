using System.ComponentModel.DataAnnotations;
using PharmazapPlus.Shared;
using PharmazapPlus.Shared.Models.Base;
using PharmazapPlus.Shared.Models.Core;
using PharmazapPlus.Shared.Models.Mensagens;

namespace PharmazapPlus.Shared.Models.CRM;

public class Etiqueta : BaseEntity
{
    public int Id { get; set; }

    public int FarmaciaId { get; set; }
    public virtual Farmacia? Farmacia { get; set; }

    [Required]
    [MaxLength(100)]
    public required string Nome { get; set; }

    [MaxLength(7)]
    public string Cor { get; set; } = Consts.EtiquetaCores.Padrao;

    public bool Ativo { get; set; } = true;

    // Relacionamentos
    public virtual ICollection<Cliente> Clientes { get; set; } = new List<Cliente>();
    public virtual ICollection<Atendimento> Atendimentos { get; set; } = new List<Atendimento>();
}
