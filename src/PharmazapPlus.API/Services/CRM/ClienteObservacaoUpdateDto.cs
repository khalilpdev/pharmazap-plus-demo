namespace PharmazapPlus.API.Services.CRM;

public record ClienteObservacaoUpdateDto
{
    public string Descricao { get; set; } = string.Empty;
    public string Prioridade { get; set; } = string.Empty;
    public bool? Concluida { get; set; }
    public DateTime? DataConclusao { get; set; }
}
