using System.Text.Json.Serialization;

namespace PharmazapPlus.Web.Services
{
    public class AtendimentoCompraAtendenteDto
    {
        [JsonPropertyName("nome")]
        public string? Nome { get; set; }
    }
}
