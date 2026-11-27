using System.ComponentModel.DataAnnotations;

namespace PharmazapPlus.API.Controllers;

public record UpdateThemeRequest
{
	[Required]
	[RegularExpression("^(Light|Dark)$", ErrorMessage = "Tema inválido. Use 'Light' ou 'Dark'.")]
	public string? Theme { get; set; }
}
