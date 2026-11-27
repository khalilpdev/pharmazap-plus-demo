namespace PharmazapPlus.API.Services.CRM;

public class CrmDashboard
{
    public int TotalClientes { get; set; }
    public int NovosClientes30Dias { get; set; }
    public int ClientesAtivos { get; set; }
    public int ClientesInativos { get; set; }
    public decimal TaxaRetencao { get; set; }
    public decimal ReceitaTotal { get; set; }
    public decimal ReceitaMediaPorCliente { get; set; }

    public Dictionary<string, int> ClientesPorSegmento { get; set; } = new();
    public Dictionary<string, int> ClientesPorStatus { get; set; } = new();
    public Dictionary<string, int> ClientesPorTipo { get; set; } = new();

    public List<TopCliente> TopClientes { get; set; } = new();
    public List<ClienteRecente> ClientesRecentes { get; set; } = new();
    public List<ClienteInativo> ClientesInativosRecentes { get; set; } = new();

    public Dictionary<DateTime, int> NovosClientesTimeline { get; set; } = new();
    public Dictionary<DateTime, decimal> ReceitaTimeline { get; set; } = new();
}
