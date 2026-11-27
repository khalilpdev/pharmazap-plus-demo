using System.Text.Json.Serialization;

namespace PharmazapPlus.Web.Services
{
    public record FarmaciaDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

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

        [JsonPropertyName("dataCadastro")]
        public DateTime DataCadastro { get; set; }

        [JsonPropertyName("plano")]
        public string Plano { get; set; } = "Basic";

        [JsonPropertyName("ativa")]
        public bool Ativa { get; set; } = true;

        [JsonPropertyName("motivoBloqueio")]
        public string? MotivoBloqueio { get; set; }

        [JsonPropertyName("totalAtendentes")]
        public int TotalAtendentes { get; set; }

        [JsonPropertyName("totalAdministradores")]
        public int TotalAdministradores { get; set; }
    }
}
