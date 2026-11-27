using System.Text.Json.Serialization;

namespace PharmazapPlus.Web.Services
{
    public record CreateFarmaciaDto
    {
        [JsonPropertyName("nome")]
        public string Nome { get; set; } = "";

        [JsonPropertyName("cnpj")]
        public string Cnpj { get; set; } = "";

        [JsonPropertyName("telefone")]
        public string? Telefone { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("endereco")]
        public string? Endereco { get; set; }

        [JsonPropertyName("logoUrl")]
        public string? LogoUrl { get; set; }

        [JsonPropertyName("nomeContato")]
        public string NomeContato { get; set; } = "";

        [JsonPropertyName("emailContato")]
        public string EmailContato { get; set; } = "";

        [JsonPropertyName("telefoneContato")]
        public string? TelefoneContato { get; set; }

        [JsonPropertyName("plano")]
        public string Plano { get; set; } = "Basic";
    }
}
