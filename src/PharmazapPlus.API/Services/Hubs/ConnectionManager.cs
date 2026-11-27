#nullable enable

using System.Collections.Concurrent;
using PharmazapPlus.API.Models.Hubs;

namespace PharmazapPlus.API.Services.Hubs;

public class ConnectionManager : IConnectionManager
{
    private readonly ConcurrentDictionary<string, HubConnectionInfo> _connections = new();
    private readonly ILogger<ConnectionManager> _logger;

    public ConnectionManager(ILogger<ConnectionManager> logger)
    {
        _logger = logger;
    }

    public void AddConnection(string userId, string connectionId, string farmaciaId, string? role)
    {
        if (string.IsNullOrEmpty(userId)) return;

        var connectionInfo = new HubConnectionInfo
        {
            UserId = userId,
            ConnectionId = connectionId,
            FarmaciaId = farmaciaId,
            Role = role,
            ConnectedAt = DateTime.UtcNow
        };

        _connections.AddOrUpdate(userId, connectionInfo, (key, existing) => connectionInfo);

        _logger.LogInformation("Connection added for user {UserId} from farmacia {FarmaciaId}", userId, farmaciaId);
    }

    public void RemoveConnection(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return;

        _connections.TryRemove(userId, out _);

        _logger.LogInformation("Connection removed for user {UserId}", userId);
    }

    public string? GetFarmaciaId(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return null;

        if (_connections.TryGetValue(userId, out var connection))
        {
            return connection.FarmaciaId;
        }

        return null;
    }

    public string? GetUserRole(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return null;

        if (_connections.TryGetValue(userId, out var connection))
        {
            return connection.Role;
        }

        return null;
    }

    public List<HubConnectionInfo> GetOnlineAtendentes(string farmaciaId)
    {
        return _connections.Values
            .Where(c => c.FarmaciaId == farmaciaId && c.Role == "Atendente")
            .ToList();
    }

    public List<HubConnectionInfo> GetOnlineUsers(string farmaciaId)
    {
        return _connections.Values
            .Where(c => c.FarmaciaId == farmaciaId)
            .ToList();
    }

    public List<HubConnectionInfo> GetAllConnections()
    {
        return _connections.Values.ToList();
    }
}
