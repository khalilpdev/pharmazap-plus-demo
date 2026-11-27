namespace PharmazapPlus.API.Controllers;

public record UpdateFarmaciaRequest
{
	public string Nome { get; set; } = "";
	public string? Cnpj { get; set; }
	public string? Telefone { get; set; }
	public string? Email { get; set; }
	public string? Endereco { get; set; }
	public string? LogoUrl { get; set; }
	public string? Plano { get; set; }
	public bool? Ativa { get; set; }
	public string? MotivoBloqueio { get; set; }
}
