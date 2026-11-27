using System.Text.Json.Serialization;

namespace PharmazapPlus.Web.Services;

public record DepartamentoDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("nome")]
    public string Nome { get; set; } = "";

    [JsonPropertyName("ativo")]
    public bool Ativo { get; set; }

    [JsonPropertyName("farmaciaId")]
    public int FarmaciaId { get; set; }
}

public record CreateDepartamentoDto
{
    [JsonPropertyName("nome")]
    public string Nome { get; set; } = "";

    [JsonPropertyName("ativo")]
    public bool? Ativo { get; set; }
}

public record UpdateDepartamentoDto
{
    [JsonPropertyName("nome")]
    public string? Nome { get; set; }

    [JsonPropertyName("ativo")]
    public bool? Ativo { get; set; }
}
