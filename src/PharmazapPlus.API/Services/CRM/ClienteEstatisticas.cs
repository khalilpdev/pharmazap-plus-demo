using PharmazapPlus.Shared.Models.CRM;

namespace PharmazapPlus.API.Services.CRM;

public class ClienteEstatisticas
{
    public int TotalAtendimentos { get; set; }
    public int TotalPedidos { get; set; }
    public decimal TotalGasto { get; set; }
    public decimal TicketMedio { get; set; }
    public DateTime? UltimaCompra { get; set; }
    public DateTime? UltimoContato { get; set; }
    public int DiasDesdeUltimoContato => UltimoContato.HasValue ?
        (int)(DateTime.UtcNow - UltimoContato.Value).TotalDays : 0;
    public Dictionary<string, int> AtendimentosPorMes { get; set; } = new();
    public Dictionary<string, decimal> ComprasPorMes { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public List<ClienteObservacao> ObservacoesPendentes { get; set; } = new();
}
