using System.Text.Json.Serialization;

namespace PharmazapPlus.Web.Services
{
    /// <summary>
    /// Wrapper interno para desserializar a resposta de dashboard-stats da API.
    /// </summary>
    internal class DashboardStatsResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("data")]
        public DashboardStats? Data { get; set; }
    }
}
