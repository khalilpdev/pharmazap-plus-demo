using System.ComponentModel.DataAnnotations;
using PharmazapPlus.Shared.Enums;

namespace PharmazapPlus.Shared.Models.Core;

public class AtividadeRecente
{
    public int Id { get; set; }

    public int FarmaciaId { get; set; }

    public int UsuarioId { get; set; }

    [MaxLength(200)]
    public string UsuarioNome { get; set; } = string.Empty;

    public TipoAtividadeEnum TipoAtividade { get; set; }

    [MaxLength(500)]
    public string Descricao { get; set; } = string.Empty;

    public int? AtendimentoId { get; set; }

    [MaxLength(200)]
    public string? ClienteNome { get; set; }

    public DateTime DataHora { get; set; } = DateTime.UtcNow;
}
