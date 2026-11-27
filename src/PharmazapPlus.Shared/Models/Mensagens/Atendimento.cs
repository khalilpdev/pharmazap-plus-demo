using System.ComponentModel.DataAnnotations.Schema;
using PharmazapPlus.Shared.Enums;
using PharmazapPlus.Shared.Models.Base;
using PharmazapPlus.Shared.Models.CRM;

namespace PharmazapPlus.Shared.Models.Mensagens;

public class Atendimento : BaseEntity
{
    public int Id { get; set; }
    public int FarmaciaId { get; set; }
    public virtual Farmacia? Farmacia { get; set; }
    public int? ClienteId { get; set; }
    public virtual Cliente? Cliente { get; set; }
    public int? AtendenteId { get; set; }
    public virtual UsuarioFarmacia? Atendente { get; set; }

    public int? EtiquetaId { get; set; }
    public virtual Etiqueta? Etiqueta { get; set; }

    public int? MotivoEncerramentoId { get; set; }
    public virtual MotivoEncerramento? MotivoEncerramento { get; set; }

    public required string WhatsAppId { get; set; } // Número do cliente

    public CanalEnum CanalEnum { get; set; } // Fonte da verdade

    [NotMapped]
    public string Canal => CanalEnum.ToString(); // Somente leitura — derivado de CanalEnum

    public AtendimentoStatusEnum StatusEnum { get; set; } // Fonte da verdade

    [NotMapped]
    public string Status => StatusEnum.ToString(); // Somente leitura — derivado de StatusEnum

    [NotMapped]
    public bool IsGrupo => WhatsAppId.EndsWith("@g.us", StringComparison.Ordinal);

    [NotMapped]
    public string? GrupoNome { get; set; }

    [NotMapped]
    public string? SenderName { get; set; }

    public DateTime DataInicio { get; set; } = DateTime.UtcNow;
    public DateTime? DataFim { get; set; }
    public int? DuracaoSegundos { get; set; }

    public int? Avaliacao { get; set; }
    public string ComentarioAvaliacao { get; set; } = string.Empty;

    /// <summary>
    /// Valor da venda registrado pelo atendente ao finalizar o atendimento (opcional).
    /// </summary>
    public decimal? ValorVenda { get; set; }

    /// <summary>
    /// URL do comprovante (imagem) anexado ao finalizar o atendimento.
    /// </summary>
    public string? ComprovanteUrl { get; set; }

    // Relacionamentos
    public ICollection<Mensagem> Mensagens { get; set; } = new List<Mensagem>();
    public ICollection<AgendamentoRecorrente> Agendamentos { get; set; } = new List<AgendamentoRecorrente>();

    // Propriedades calculadas
    public bool EstaAtivo => StatusEnum == AtendimentoStatusEnum.EmAndamento || StatusEnum == AtendimentoStatusEnum.Pendente;
    public int TotalMensagens => Mensagens?.Count ?? 0;
    public DateTime? UltimaMensagemAt => Mensagens?.OrderByDescending(m => m.ReceivedAt).FirstOrDefault()?.ReceivedAt;

    public void Finalizar()
    {
        if (DataFim == null)
        {
            DataFim = DateTime.UtcNow;
            DuracaoSegundos = (int)(DataFim.Value - DataInicio).TotalSeconds;
            StatusEnum = AtendimentoStatusEnum.Finalizado;
        }
    }
}
