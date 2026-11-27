namespace PharmazapPlus.API.Services.Instagram;

public record InstagramPhotoPublishResponse
{
    public bool Success { get; init; }
    public string? MediaId { get; init; }
    public DateTime Timestamp { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}
