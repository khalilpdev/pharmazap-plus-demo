namespace PharmazapPlus.Shared.Models.CRM;

/// <summary>
/// Resultado de importação de dados
/// </summary>
public class ImportResult
{
    public bool Success { get; set; }
    public int ImportedRows { get; set; }
    public int FailedRows { get; set; }
    public int TotalRows { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<Cliente> ImportedClientes { get; set; } = new();
}
