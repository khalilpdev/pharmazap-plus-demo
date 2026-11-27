namespace PharmazapPlus.API.Models.Hubs;

public class HubConnectionInfo
{
    public string UserId { get; set; } = null!;
    public string ConnectionId { get; set; } = null!;
    public string FarmaciaId { get; set; } = null!;
    public string? Role { get; set; }
    public DateTime ConnectedAt { get; set; }
    public DateTime LastActivity { get; set; }
}