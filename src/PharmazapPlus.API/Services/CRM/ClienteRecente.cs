namespace PharmazapPlus.API.Services.CRM;

public class ClienteRecente
{
    public int ClienteId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public DateTime DataCadastro { get; set; }
    public string OrigemCadastro { get; set; } = string.Empty;
    public int TotalAtendimentos { get; set; }
}
