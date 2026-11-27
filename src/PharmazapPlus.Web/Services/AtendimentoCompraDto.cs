using System.Text.Json.Serialization;

namespace PharmazapPlus.Web.Services
{
    public class AtendimentoCompraDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("dataInicio")]
        public DateTime DataInicio { get; set; }

        [JsonPropertyName("dataFim")]
        public DateTime? DataFim { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("valorVenda")]
        public decimal? ValorVenda { get; set; }

        [JsonPropertyName("comentarioAvaliacao")]
        public string? ComentarioAvaliacao { get; set; }

        [JsonPropertyName("atendente")]
        public AtendimentoCompraAtendenteDto? Atendente { get; set; }
    }
}
