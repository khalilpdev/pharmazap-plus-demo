using System.Text.Json.Serialization;
using PharmazapPlus.Shared.Models.CRM;

namespace PharmazapPlus.Web.Services;

internal class ClienteListResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public List<Cliente>? Data { get; set; }

    [JsonPropertyName("pagination")]
    public PaginationInfo? Pagination { get; set; }
}

internal class PaginationInfo
{
    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }
}
