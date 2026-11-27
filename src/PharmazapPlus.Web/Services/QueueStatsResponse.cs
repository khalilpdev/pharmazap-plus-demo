using System.Text.Json.Serialization;
using PharmazapPlus.Shared.Models.Mensagens;

namespace PharmazapPlus.Web.Services;

internal class QueueStatsResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public QueueStats? Data { get; set; }
}
