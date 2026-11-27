namespace PharmazapPlus.API.Services.CRM;

public class ClienteInativo
{
    public int ClienteId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public DateTime? UltimoContato { get; set; }
    public int DiasInativo { get; set; }
    public decimal TotalGastoHistorico { get; set; }
}
