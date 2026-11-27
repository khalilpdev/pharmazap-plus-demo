using System.Text.Json.Serialization;

namespace PharmazapPlus.Web.Services
{
    public record MotivoEncerramentoDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("nome")]
        public string Nome { get; set; } = "";

        [JsonPropertyName("exigeValorVenda")]
        public bool ExigeValorVenda { get; set; }

        [JsonPropertyName("exigeComprovante")]
        public bool ExigeComprovante { get; set; }

        [JsonPropertyName("ativo")]
        public bool Ativo { get; set; }

        [JsonPropertyName("ordem")]
        public int Ordem { get; set; }

        [JsonPropertyName("farmaciaId")]
        public int FarmaciaId { get; set; }
    }

    public record CreateMotivoEncerramentoDto
    {
        [JsonPropertyName("nome")]
        public string Nome { get; set; } = "";

        [JsonPropertyName("exigeValorVenda")]
        public bool? ExigeValorVenda { get; set; }

        [JsonPropertyName("exigeComprovante")]
        public bool? ExigeComprovante { get; set; }

        [JsonPropertyName("ativo")]
        public bool? Ativo { get; set; }

        [JsonPropertyName("ordem")]
        public int? Ordem { get; set; }
    }

    public record UpdateMotivoEncerramentoDto
    {
        [JsonPropertyName("nome")]
        public string? Nome { get; set; }

        [JsonPropertyName("exigeValorVenda")]
        public bool? ExigeValorVenda { get; set; }

        [JsonPropertyName("exigeComprovante")]
        public bool? ExigeComprovante { get; set; }

        [JsonPropertyName("ativo")]
        public bool? Ativo { get; set; }

        [JsonPropertyName("ordem")]
        public int? Ordem { get; set; }
    }
}
