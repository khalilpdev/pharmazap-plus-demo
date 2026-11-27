using System.Text.Json.Serialization;

namespace PharmazapPlus.Web.Services
{
    public record MensagemPreDefinidaDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("titulo")]
        public string Titulo { get; set; } = "";

        [JsonPropertyName("texto")]
        public string Texto { get; set; } = "";

        [JsonPropertyName("ativo")]
        public bool Ativo { get; set; }

        [JsonPropertyName("ordem")]
        public int Ordem { get; set; }

        [JsonPropertyName("farmaciaId")]
        public int FarmaciaId { get; set; }
    }

    public record CreateMensagemPreDefinidaDto
    {
        [JsonPropertyName("titulo")]
        public string Titulo { get; set; } = "";

        [JsonPropertyName("texto")]
        public string Texto { get; set; } = "";

        [JsonPropertyName("ativo")]
        public bool? Ativo { get; set; }

        [JsonPropertyName("ordem")]
        public int? Ordem { get; set; }
    }

    public record UpdateMensagemPreDefinidaDto
    {
        [JsonPropertyName("titulo")]
        public string? Titulo { get; set; }

        [JsonPropertyName("texto")]
        public string? Texto { get; set; }

        [JsonPropertyName("ativo")]
        public bool? Ativo { get; set; }

        [JsonPropertyName("ordem")]
        public int? Ordem { get; set; }
    }
}
