using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using PharmazapPlus.API.BackgroundServices;
using PharmazapPlus.API.Models;
using PharmazapPlus.API.Services.Monitoring;

namespace PharmazapPlus.API.Tests.BackgroundServices;

public class ServerHealthMonitorServiceTests
{
    private readonly Mock<HealthCheckService> _healthCheckServiceMock;
    private readonly Mock<IServerMonitorNotificationService> _notificationServiceMock;
    private readonly Mock<ILogger<ServerHealthMonitorService>> _loggerMock;
    private readonly HealthMonitorConfig _config;

    public ServerHealthMonitorServiceTests()
    {
        _healthCheckServiceMock = new Mock<HealthCheckService>();
        _notificationServiceMock = new Mock<IServerMonitorNotificationService>();
        _loggerMock = new Mock<ILogger<ServerHealthMonitorService>>();

        _config = new HealthMonitorConfig
        {
            Enabled = true,
            CheckIntervalSeconds = 60,
            FailureThreshold = 3,
            AlertCooldownMinutes = 30,
            NotifyViaWhatsApp = true,
            NotifyViaEmail = true,
            ServerName = "TestServer",
            FetchAdminsFromDatabase = false
        };
    }

    private ServerHealthMonitorService CreateService(HealthMonitorConfig? config = null)
    {
        var options = Options.Create(config ?? _config);
        return new ServerHealthMonitorService(
            _healthCheckServiceMock.Object,
            _notificationServiceMock.Object,
            options,
            _loggerMock.Object);
    }

    private void SetupHealthy()
    {
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["database"] = new HealthReportEntry(HealthStatus.Healthy, null, TimeSpan.Zero, null, null)
            },
            HealthStatus.Healthy,
            TimeSpan.Zero);

        _healthCheckServiceMock
            .Setup(x => x.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
    }

    private void SetupUnhealthy(params string[] failedChecks)
    {
        var entries = failedChecks.ToDictionary(
            check => check,
            check => new HealthReportEntry(HealthStatus.Unhealthy, $"{check} failed", TimeSpan.Zero, null, null));

        var report = new HealthReport(entries, HealthStatus.Unhealthy, TimeSpan.Zero);

        _healthCheckServiceMock
            .Setup(x => x.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenHealthy_DoesNotSendNotification()
    {
        // Arrange
        SetupHealthy();
        var service = CreateService();

        // Act
        await service.CheckHealthAsync();

        // Assert
        _notificationServiceMock.Verify(
            x => x.NotifyServerDown(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<DateTime>()),
            Times.Never);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenUnhealthyBelowThreshold_DoesNotSendNotification()
    {
        // Arrange
        SetupUnhealthy("database");
        var config = new HealthMonitorConfig
        {
            Enabled = true,
            FailureThreshold = 3,
            AlertCooldownMinutes = 30,
            ServerName = "TestServer",
            FetchAdminsFromDatabase = false
        };
        var service = CreateService(config);

        // Act — 2 failures (below threshold of 3)
        await service.CheckHealthAsync();
        await service.CheckHealthAsync();

        // Assert
        _notificationServiceMock.Verify(
            x => x.NotifyServerDown(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<DateTime>()),
            Times.Never);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenUnhealthyReachesThreshold_SendsNotification()
    {
        // Arrange
        SetupUnhealthy("database");
        var config = new HealthMonitorConfig
        {
            Enabled = true,
            FailureThreshold = 3,
            AlertCooldownMinutes = 30,
            ServerName = "TestServer",
            FetchAdminsFromDatabase = false
        };
        var service = CreateService(config);

        _notificationServiceMock
            .Setup(x => x.NotifyServerDown(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        // Act — reach the failure threshold
        await service.CheckHealthAsync();
        await service.CheckHealthAsync();
        await service.CheckHealthAsync();

        // Assert
        _notificationServiceMock.Verify(
            x => x.NotifyServerDown("TestServer", It.Is<IEnumerable<string>>(c => c.Contains("database")), It.IsAny<DateTime>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenUnhealthyPassesThreshold_SendsNotificationOnlyOnce()
    {
        // Arrange
        SetupUnhealthy("database");
        var config = new HealthMonitorConfig
        {
            Enabled = true,
            FailureThreshold = 2,
            AlertCooldownMinutes = 60,
            ServerName = "TestServer",
            FetchAdminsFromDatabase = false
        };
        var service = CreateService(config);

        _notificationServiceMock
            .Setup(x => x.NotifyServerDown(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        // Act — 4 failures (all within cooldown window)
        await service.CheckHealthAsync();
        await service.CheckHealthAsync();
        await service.CheckHealthAsync();
        await service.CheckHealthAsync();

        // Assert — notification sent only once due to cooldown
        _notificationServiceMock.Verify(
            x => x.NotifyServerDown(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<DateTime>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenRecoveredAfterDown_SendsRecoveryNotification()
    {
        // Arrange
        SetupUnhealthy("database");
        var config = new HealthMonitorConfig
        {
            Enabled = true,
            FailureThreshold = 2,
            AlertCooldownMinutes = 60,
            ServerName = "TestServer",
            FetchAdminsFromDatabase = false
        };
        var service = CreateService(config);

        _notificationServiceMock
            .Setup(x => x.NotifyServerDown(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);
        _notificationServiceMock
            .Setup(x => x.NotifyServerRecovered(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);

        // Act — go down, then recover
        await service.CheckHealthAsync();
        await service.CheckHealthAsync(); // reaches threshold → alert sent

        SetupHealthy();
        await service.CheckHealthAsync(); // recovery

        // Assert
        _notificationServiceMock.Verify(
            x => x.NotifyServerDown(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<DateTime>()),
            Times.Once);
        _notificationServiceMock.Verify(
            x => x.NotifyServerRecovered("TestServer", It.IsAny<TimeSpan>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenRecoveredBeforeThreshold_DoesNotSendRecoveryNotification()
    {
        // Arrange
        SetupUnhealthy("database");
        var config = new HealthMonitorConfig
        {
            Enabled = true,
            FailureThreshold = 3,
            AlertCooldownMinutes = 60,
            ServerName = "TestServer",
            FetchAdminsFromDatabase = false
        };
        var service = CreateService(config);

        // Act — 1 failure then recover (never reached threshold)
        await service.CheckHealthAsync();

        SetupHealthy();
        await service.CheckHealthAsync();

        // Assert — no notifications at all
        _notificationServiceMock.Verify(
            x => x.NotifyServerDown(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<DateTime>()),
            Times.Never);
        _notificationServiceMock.Verify(
            x => x.NotifyServerRecovered(It.IsAny<string>(), It.IsAny<TimeSpan>()),
            Times.Never);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenMultipleChecksFailed_ReportsAllFailedChecks()
    {
        // Arrange
        SetupUnhealthy("database", "redis");
        var config = new HealthMonitorConfig
        {
            Enabled = true,
            FailureThreshold = 1,
            AlertCooldownMinutes = 60,
            ServerName = "TestServer",
            FetchAdminsFromDatabase = false
        };
        var service = CreateService(config);

        IEnumerable<string>? capturedChecks = null;
        _notificationServiceMock
            .Setup(x => x.NotifyServerDown(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<DateTime>()))
            .Callback<string, IEnumerable<string>, DateTime>((_, checks, _) => capturedChecks = checks)
            .Returns(Task.CompletedTask);

        // Act
        await service.CheckHealthAsync();

        // Assert
        capturedChecks.Should().NotBeNull();
        capturedChecks.Should().Contain("database");
        capturedChecks.Should().Contain("redis");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenExceptionThrown_DoesNotThrow()
    {
        // Arrange
        _healthCheckServiceMock
            .Setup(x => x.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test exception"));

        var service = CreateService();

        // Act & Assert
        var act = async () => await service.CheckHealthAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CheckHealthAsync_WhenNotificationFails_DoesNotThrow()
    {
        // Arrange
        SetupUnhealthy("database");
        var config = new HealthMonitorConfig
        {
            Enabled = true,
            FailureThreshold = 1,
            AlertCooldownMinutes = 60,
            ServerName = "TestServer",
            FetchAdminsFromDatabase = false
        };
        var service = CreateService(config);

        _notificationServiceMock
            .Setup(x => x.NotifyServerDown(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<DateTime>()))
            .ThrowsAsync(new Exception("Notification error"));

        // Act & Assert
        var act = async () => await service.CheckHealthAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CheckHealthAsync_WhenDisabled_StopsExecutionImmediately()
    {
        // Arrange
        var config = new HealthMonitorConfig { Enabled = false, ServerName = "TestServer" };
        var service = CreateService(config);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        // Act — ExecuteAsync should return quickly when disabled
        var act = async () =>
        {
            var method = typeof(ServerHealthMonitorService)
                .GetMethod("ExecuteAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            await (Task)method.Invoke(service, [cts.Token])!;
        };

        // Assert — completes without exception and without checking health
        await act.Should().NotThrowAsync();
        _healthCheckServiceMock.Verify(
            x => x.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
