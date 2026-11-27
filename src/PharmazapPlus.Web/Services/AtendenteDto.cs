using System.Text.Json.Serialization;

namespace PharmazapPlus.Web.Services
{
    public record AtendenteDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("nome")]
        public string Nome { get; set; } = "";

        [JsonPropertyName("email")]
        public string Email { get; set; } = "";

        [JsonPropertyName("telefone")]
        public string? Telefone { get; set; }

        [JsonPropertyName("ativo")]
        public bool Ativo { get; set; }

        [JsonPropertyName("dataCadastro")]
        public DateTime DataCadastro { get; set; }

        [JsonPropertyName("ultimoLogin")]
        public DateTime? UltimoLogin { get; set; }

        [JsonPropertyName("turno")]
        public string? Turno { get; set; }

        [JsonPropertyName("recebeNotificacoes")]
        public bool RecebeNotificacoes { get; set; }

        [JsonPropertyName("totalAtendimentos")]
        public int TotalAtendimentos { get; set; }

        [JsonPropertyName("atendimentosHoje")]
        public int AtendimentosHoje { get; set; }

        [JsonPropertyName("farmaciaId")]
        public int FarmaciaId { get; set; }

        [JsonPropertyName("departamentoId")]
        public int? DepartamentoId { get; set; }

        [JsonPropertyName("departamentoNome")]
        public string? DepartamentoNome { get; set; }
    }
}
