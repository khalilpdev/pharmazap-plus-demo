using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;
using PharmazapPlus.Shared.Enums;

namespace PharmazapPlus.Shared.Models.Campanhas;

public class Campanha
{
    public int Id { get; set; }
    public int FarmaciaId { get; set; }
    [JsonIgnore]
    public virtual Farmacia? Farmacia { get; set; }

    [Required]
    [MaxLength(200)]
    public required string Nome { get; set; }

    [Required]
    [MaxLength(2000)]
    public required string Mensagem { get; set; }

    public CampanhaTipoEnum Tipo { get; set; } = CampanhaTipoEnum.Promocional;
    public CampanhaStatusEnum Status { get; set; } = CampanhaStatusEnum.Rascunho;

    // Segmentação de destinatários
    /// <summary>Enviar para todos os clientes ativos da farmácia</summary>
    public bool EnviarParaTodosAtivos { get; set; } = false;

    /// <summary>Enviar somente para clientes com UsoContinuo = true ou medicamentos contínuos cadastrados</summary>
    public bool EnviarParaUsoContinuo { get; set; } = false;

    /// <summary>Respeitar a flag AceitaMarketing do cliente</summary>
    public bool EnviarParaAceitaMarketing { get; set; } = true;

    /// <summary>JSON array de valores ClientePerfilCompraEnum (null = todos os perfis)</summary>
    [Column(TypeName = "jsonb")]
    public string? FiltroPerfilCompra { get; set; }

    /// <summary>JSON array de IDs de Etiqueta para filtrar destinatários. Null = sem filtro por etiqueta</summary>
    [Column(TypeName = "jsonb")]
    public string? FiltroEtiquetas { get; set; }

    /// <summary>Lista de IDs de etiqueta selecionados para filtrar destinatários (null/empty = sem filtro)</summary>
    [NotMapped]
    public List<int> EtiquetaIds
    {
        get => string.IsNullOrEmpty(FiltroEtiquetas)
            ? new List<int>()
            : JsonSerializer.Deserialize<List<int>>(FiltroEtiquetas) ?? new List<int>();
        set => FiltroEtiquetas = value?.Count > 0 ? JsonSerializer.Serialize(value) : null;
    }

    // Agendamento
    /// <summary>Null = disparo imediato ao executar</summary>
    public DateTime? DataAgendamento { get; set; }
    public DateTime? DataExecucao { get; set; }
    public DateTime? DataConclusao { get; set; }

    // Resultados
    public int TotalDestinatarios { get; set; } = 0;
    public int TotalEnviados { get; set; } = 0;
    public int TotalErros { get; set; } = 0;

    [MaxLength(500)]
    public string? Observacoes { get; set; }

    // Auditoria
    [MaxLength(100)]
    public string CriadoPor { get; set; } = "Sistema";
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? AtualizadoEm { get; set; }

    [MaxLength(100)]
    public string? AtualizadoPor { get; set; }

    // Propriedades calculadas
    [NotMapped]
    public bool PodeExecutar => Status == CampanhaStatusEnum.Agendada &&
                                (DataAgendamento == null || DataAgendamento <= DateTime.UtcNow);

    [NotMapped]
    public bool EstaAtiva => Status is CampanhaStatusEnum.Rascunho or CampanhaStatusEnum.Agendada;
}
