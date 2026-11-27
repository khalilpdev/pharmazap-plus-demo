namespace PharmazapPlus.API.Controllers;

public record SimularMensagemRequest
{
    public int FarmaciaId { get; set; }
    public string From { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public bool IsGroup { get; set; }
}
