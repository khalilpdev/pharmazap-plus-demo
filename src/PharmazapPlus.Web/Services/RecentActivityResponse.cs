using System.Text.Json.Serialization;

namespace PharmazapPlus.Web.Services;

internal class RecentActivityResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public List<RecentActivityItem>? Data { get; set; }
}
