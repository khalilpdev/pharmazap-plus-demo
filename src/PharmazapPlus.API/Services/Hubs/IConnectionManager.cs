using PharmazapPlus.API.Models.Hubs;

namespace PharmazapPlus.API.Services.Hubs;

public interface IConnectionManager
{
    void AddConnection(string userId, string connectionId, string farmaciaId, string? role);
    void RemoveConnection(string userId);
    string? GetFarmaciaId(string userId);
    string? GetUserRole(string userId);
    List<HubConnectionInfo> GetOnlineAtendentes(string farmaciaId);
    List<HubConnectionInfo> GetOnlineUsers(string farmaciaId);
    List<HubConnectionInfo> GetAllConnections();
}