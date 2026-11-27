using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PharmazapPlus.Shared.Models.Vendas;

public class PedidoItem
{
    public int Id { get; set; }
    public int PedidoId { get; set; }
    public required Pedido Pedido { get; set; }

    // Produto
    [Required]
    [MaxLength(50)]
    public required string ProdutoCodigo { get; set; }

    [Required]
    [MaxLength(200)]
    public required string ProdutoNome { get; set; }

    [MaxLength(500)]
    public required string ProdutoDescricao { get; set; }

    // Quantidade e valor
    [Required]
    [Range(1, 999)]
    public int Quantidade { get; set; }

    [Required]
    [Range(0, 99999.99)]
    public decimal PrecoUnitario { get; set; }

    [Range(0, 99999.99)]
    public decimal Desconto { get; set; }

    [NotMapped]
    public decimal Total => (PrecoUnitario * Quantidade) - Desconto;

    // Informações médicas
    [MaxLength(50)]
    public required string Dosagem { get; set; }

    [MaxLength(200)]
    public required string Posologia { get; set; }

    [MaxLength(500)]
    public required string Observacoes { get; set; }
}