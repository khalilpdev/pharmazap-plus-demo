using PharmazapPlus.Shared.Models.CRM;

namespace PharmazapPlus.API.Services.CRM;

public class ClienteExportFilter
{
    public ClienteStatus? Status { get; set; }
    public ClienteTipo? Tipo { get; set; }
    public string Segmento { get; set; } = string.Empty;
    public DateTime? DataCadastroInicio { get; set; }
    public DateTime? DataCadastroFim { get; set; }
    public bool? UsoContinuo { get; set; }
    public bool IncludeInativos { get; set; } = false;
    public List<string> Campos { get; set; } = new();
}
