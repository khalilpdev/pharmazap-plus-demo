using System.Text.Json.Serialization;

namespace PharmazapPlus.Web.Services
{
    public record UpdateFarmaciaDto
    {
        [JsonPropertyName("nome")]
        public string Nome { get; set; } = "";

        [JsonPropertyName("cnpj")]
        public string? Cnpj { get; set; }

        [JsonPropertyName("telefone")]
        public string? Telefone { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("endereco")]
        public string? Endereco { get; set; }

        [JsonPropertyName("logoUrl")]
        public string? LogoUrl { get; set; }

        [JsonPropertyName("plano")]
        public string? Plano { get; set; }

        [JsonPropertyName("ativa")]
        public bool? Ativa { get; set; }

        [JsonPropertyName("motivoBloqueio")]
        public string? MotivoBloqueio { get; set; }
    }
}
