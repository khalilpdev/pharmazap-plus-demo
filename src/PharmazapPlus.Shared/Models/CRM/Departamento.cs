using System.ComponentModel.DataAnnotations;
using PharmazapPlus.Shared.Models.Base;
using PharmazapPlus.Shared.Models.Core;

namespace PharmazapPlus.Shared.Models.CRM;

public class Departamento : BaseEntity
{
    public int Id { get; set; }

    public int FarmaciaId { get; set; }
    public virtual Farmacia? Farmacia { get; set; }

    [Required]
    [MaxLength(100)]
    public required string Nome { get; set; }

    public bool Ativo { get; set; } = true;
}
