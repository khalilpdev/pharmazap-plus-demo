using PharmazapPlus.Shared.Models.CRM;

namespace PharmazapPlus.API.Services.CRM;

public class ClienteSearchFilter
{
    public string Query { get; set; } = string.Empty;
    public ClienteStatus? Status { get; set; }
    public ClienteTipo? Tipo { get; set; }
    public string Segmento { get; set; } = string.Empty;
    public bool? UsoContinuo { get; set; }
    public string Tag { get; set; } = string.Empty;
    public DateTime? DataInicio { get; set; }
    public DateTime? DataFim { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string OrderBy { get; set; } = "Nome";
    public bool OrderDesc { get; set; } = false;
}
