namespace PharmazapPlus.Shared.Models.CRM;

public class ClienteTag
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public required Cliente Cliente { get; set; }
    public required string Tag { get; set; } // Ex: "Diabetes", "Hipertenso", "Gestante", "Idoso"
    public string Cor { get; set; } = "#0078d4"; // Cor da tag
    public DateTime DataAdicao { get; set; } = DateTime.UtcNow;
    public required string AdicionadoPor { get; set; }
}