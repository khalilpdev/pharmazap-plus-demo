namespace PharmazapPlus.API.Services.CRM;

public class ClienteSegmento
{
    public string Nome { get; set; } = string.Empty;
    public int Quantidade { get; set; }
    public decimal ReceitaTotal { get; set; }
    public decimal ReceitaMedia { get; set; }
}
