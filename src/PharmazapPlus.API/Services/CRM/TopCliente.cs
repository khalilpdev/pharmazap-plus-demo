namespace PharmazapPlus.API.Services.CRM;

public class TopCliente
{
    public int ClienteId { get; set; }
    public string Nome { get; set; } = null!;
    public decimal TotalGasto { get; set; }
    public int TotalPedidos { get; set; }
    public string Segmento { get; set; } = null!;
}
