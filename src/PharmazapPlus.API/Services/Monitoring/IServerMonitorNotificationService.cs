namespace PharmazapPlus.API.Services.Monitoring;

public interface IServerMonitorNotificationService
{
    Task NotifyServerDown(string serverName, IEnumerable<string> failedChecks, DateTime detectedAt);
    Task NotifyServerRecovered(string serverName, TimeSpan downtime);
}
