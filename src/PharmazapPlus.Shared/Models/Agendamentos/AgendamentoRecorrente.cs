using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmazapPlus.Shared.Models.Mensagens;

namespace PharmazapPlus.Shared.Models.Agendamentos;

public class AgendamentoRecorrente
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public required Cliente Cliente { get; set; }
    public int FarmaciaId { get; set; }
    public required Farmacia Farmacia { get; set; }
    public int? AtendimentoId { get; set; }
    public required Atendimento Atendimento { get; set; }

    [Required]
    [MaxLength(200)]
    public required string Medicamento { get; set; }

    [Required]
    public DateTime ProximoDisparo { get; set; }

    [Required]
    [Range(1, 365)]
    public int FrequenciaDias { get; set; } = 30;

    // Configurações
    [MaxLength(50)]
    public required string Dosagem { get; set; }

    [MaxLength(200)]
    public required string Posologia { get; set; } // "Tomar 1 comprimido após o café"

    public bool Ativo { get; set; } = true;

    // Notificações
    public bool NotificarCliente { get; set; } = true;
    public bool NotificarAtendente { get; set; } = true;

    [MaxLength(20)]
    public required string HorarioPreferencial { get; set; } // "09:00", "14:00"

    // Histórico
    public int QuantidadeDisparos { get; set; } = 0;
    public DateTime? UltimoDisparo { get; set; }

    // Datas
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataCancelamento { get; set; }

    [MaxLength(200)]
    public required string CriadoPor { get; set; }

    [MaxLength(500)]
    public required string Observacoes { get; set; }

    // Propriedades calculadas
    [NotMapped]
    public bool PrecisaDisparar => Ativo && ProximoDisparo <= DateTime.UtcNow;

    [NotMapped]
    public int DiasRestantes => (int)(ProximoDisparo - DateTime.UtcNow).TotalDays;

    public void MarcarComoDisparado()
    {
        UltimoDisparo = DateTime.UtcNow;
        QuantidadeDisparos++;
        ProximoDisparo = ProximoDisparo.AddDays(FrequenciaDias);
    }
}