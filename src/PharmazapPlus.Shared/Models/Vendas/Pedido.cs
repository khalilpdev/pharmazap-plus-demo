// PharmazapPlus.Shared/Models/Pedido.cs
using PharmazapPlus.Shared.Enums;
using PharmazapPlus.Shared.Models.CRM;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PharmazapPlus.Shared.Models.Vendas;

public class Pedido
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public required Cliente Cliente { get; set; }
    public int FarmaciaId { get; set; }
    public required Farmacia Farmacia { get; set; }

    // Link com Atendimento
    public int? AtendimentoId { get; set; }

    // Atendente responsável (pode diferir do cliente do pedido)
    public int? AtendenteId { get; set; }

    // Identificação
    [Required]
    [MaxLength(50)]
    public required string NumeroPedido { get; set; }

    // Datas
    public DateTime DataPedido { get; set; } = DateTime.UtcNow;
    public DateTime? DataEntrega { get; set; }

    // Status
    public PedidoStatusEnum Status { get; set; } = PedidoStatusEnum.Pendente;

    public PedidoTipoEnum TipoPedido { get; set; } = PedidoTipoEnum.Delivery;

    public FormaPagamentoEnum FormaPagamento { get; set; } = FormaPagamentoEnum.NaoEspecificado;

    // Valores
    [Required]
    [Range(0, 999999.99)]
    public decimal ValorTotal { get; set; }

    [Range(0, 999999.99)]
    public decimal Desconto { get; set; }

    [NotMapped]
    public decimal ValorFinal => ValorTotal - Desconto;

    // Informações adicionais
    [MaxLength(500)]
    public required string PrescricaoMedica { get; set; } // URL do arquivo

    [MaxLength(1000)]
    public required string Observacoes { get; set; }

    // Entrega
    [MaxLength(500)]
    public required string EnderecoEntrega { get; set; }

    [MaxLength(100)]
    public required string Entregador { get; set; }

    // Relacionamentos
    public virtual ICollection<PedidoItem> Itens { get; set; } = new List<PedidoItem>();
}