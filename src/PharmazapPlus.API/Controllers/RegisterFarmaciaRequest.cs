namespace PharmazapPlus.API.Controllers;

public record RegisterFarmaciaRequest
{
	public string Nome { get; set; } = string.Empty;
	public string Cnpj { get; set; } = string.Empty;
	public string Telefone { get; set; } = string.Empty;
	public string Email { get; set; } = string.Empty;
	public string Endereco { get; set; } = string.Empty;
	public string? LogoUrl { get; set; }
	public string NomeContato { get; set; } = string.Empty;
	public string EmailContato { get; set; } = string.Empty;
	public string TelefoneContato { get; set; } = string.Empty;
	public string Plano { get; set; } = "Basic";
}
