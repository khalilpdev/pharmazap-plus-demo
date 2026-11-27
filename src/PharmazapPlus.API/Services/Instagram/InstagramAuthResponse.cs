namespace PharmazapPlus.API.Services.Instagram;

public record InstagramAuthResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}
