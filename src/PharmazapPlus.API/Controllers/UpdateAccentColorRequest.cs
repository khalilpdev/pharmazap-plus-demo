using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace PharmazapPlus.API.Controllers;

public record UpdateAccentColorRequest
{
	/// <summary>
	/// HTML hex color string (e.g. "#0078d4"), or null / empty to restore the default accent color.
	/// </summary>
	[MaxLength(20)]
	[RegularExpression(@"^(#[0-9a-fA-F]{3,6})?$", ErrorMessage = "Cor inválida. Use formato hexadecimal (#rrggbb ou #rgb) ou vazio para restaurar o padrão.")]
	public string? AccentColor { get; set; }
}
