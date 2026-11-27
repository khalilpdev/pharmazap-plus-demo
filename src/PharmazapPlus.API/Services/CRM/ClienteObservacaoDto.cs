namespace PharmazapPlus.API.Services.CRM;

public record ClienteObservacaoDto
{
    public string Titulo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Prioridade { get; set; } = string.Empty;
    public string Responsavel { get; set; } = string.Empty;
}
