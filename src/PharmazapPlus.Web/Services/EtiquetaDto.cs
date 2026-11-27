using System.Text.Json.Serialization;

namespace PharmazapPlus.Web.Services
{
    public record EtiquetaDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("nome")]
        public string Nome { get; set; } = "";

        [JsonPropertyName("cor")]
        public string Cor { get; set; } = PharmazapPlus.Shared.Consts.EtiquetaCores.Padrao;

        [JsonPropertyName("ativo")]
        public bool Ativo { get; set; }

        [JsonPropertyName("farmaciaId")]
        public int FarmaciaId { get; set; }
    }

    public record CreateEtiquetaDto
    {
        [JsonPropertyName("nome")]
        public string Nome { get; set; } = "";

        [JsonPropertyName("cor")]
        public string? Cor { get; set; }

        [JsonPropertyName("ativo")]
        public bool? Ativo { get; set; }
    }

    public record UpdateEtiquetaDto
    {
        [JsonPropertyName("nome")]
        public string? Nome { get; set; }

        [JsonPropertyName("cor")]
        public string? Cor { get; set; }

        [JsonPropertyName("ativo")]
        public bool? Ativo { get; set; }
    }
}
