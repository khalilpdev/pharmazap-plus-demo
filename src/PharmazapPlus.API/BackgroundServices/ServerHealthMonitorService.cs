using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using PharmazapPlus.API.Models;
using PharmazapPlus.API.Services.Monitoring;

namespace PharmazapPlus.API.BackgroundServices;

public class ServerHealthMonitorService : BackgroundService
{
    private readonly HealthCheckService _healthCheckService;
    private readonly IServerMonitorNotificationService _notificationService;
    private readonly HealthMonitorConfig _config;
    private readonly ILogger<ServerHealthMonitorService> _logger;

    private int _consecutiveFailures = 0;
    private DateTime? _lastAlertSent = null;
    private DateTime? _serverDownSince = null;
    private bool _isCurrentlyDown = false;

    public ServerHealthMonitorService(
        HealthCheckService healthCheckService,
        IServerMonitorNotificationService notificationService,
        IOptions<HealthMonitorConfig> config,
        ILogger<ServerHealthMonitorService> logger)
    {
        _healthCheckService = healthCheckService;
        _notificationService = notificationService;
        _config = config.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.Enabled)
        {
            _logger.LogInformation("Server Health Monitor está desabilitado");
            return;
        }

        _logger.LogInformation(
            "Server Health Monitor iniciado. Intervalo: {Interval}s, Threshold: {Threshold} falhas consecutivas",
            _config.CheckIntervalSeconds, _config.FailureThreshold);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_config.CheckIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await CheckHealthAsync();
        }
    }

    internal async Task CheckHealthAsync()
    {
        try
        {
            var report = await _healthCheckService.CheckHealthAsync();
            var isHealthy = report.Status != HealthStatus.Unhealthy;

            if (!isHealthy)
            {
                _consecutiveFailures++;

                var failedChecks = report.Entries
                    .Where(e => e.Value.Status == HealthStatus.Unhealthy)
                    .Select(e => e.Key)
                    .ToList();

                _logger.LogWarning(
                    "Health check falhou ({Count} vez(es) consecutiva(s)). Problemas: {Checks}",
                    _consecutiveFailures, string.Join(", ", failedChecks));

                if (_consecutiveFailures >= _config.FailureThreshold)
                {
                    if (!_isCurrentlyDown)
                    {
                        _isCurrentlyDown = true;
                        _serverDownSince = DateTime.UtcNow;
                    }

                    var shouldAlert = _lastAlertSent == null ||
                        DateTime.UtcNow - _lastAlertSent > TimeSpan.FromMinutes(_config.AlertCooldownMinutes);

                    if (shouldAlert)
                    {
                        _lastAlertSent = DateTime.UtcNow;
                        _logger.LogCritical(
                            "Servidor detectado como fora do ar após {Failures} falhas. Enviando alertas para administradores",
                            _consecutiveFailures);

                        await _notificationService.NotifyServerDown(
                            _config.ServerName,
                            failedChecks,
                            _serverDownSince ?? DateTime.UtcNow);
                    }
                }
            }
            else
            {
                if (_isCurrentlyDown)
                {
                    if (_serverDownSince == null)
                    {
                        _logger.LogWarning("_serverDownSince estava nulo ao recuperar — usando DateTime.UtcNow como fallback");
                    }
                    var downtime = DateTime.UtcNow - (_serverDownSince ?? DateTime.UtcNow);
                    _logger.LogInformation("Servidor recuperado após {Downtime}", downtime);

                    _isCurrentlyDown = false;
                    _serverDownSince = null;
                    _lastAlertSent = null;

                    await _notificationService.NotifyServerRecovered(_config.ServerName, downtime);
                }

                if (_consecutiveFailures > 0)
                {
                    _logger.LogInformation(
                        "Health check recuperado após {Failures} falha(s) consecutiva(s)",
                        _consecutiveFailures);
                    _consecutiveFailures = 0;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar saúde do servidor");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Server Health Monitor parando");
        await base.StopAsync(cancellationToken);
    }
}
