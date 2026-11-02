using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using PulseBoard.Api.Contracts;
using PulseBoard.Api.Domain;
using PulseBoard.Api.Telemetry;

namespace PulseBoard.Api.Tests;

/// <summary>
/// Drives samples through the processor exactly as the worker would and checks the
/// resulting device state, alert lifecycle, REST views and SignalR broadcasts.
/// Each test owns a factory so device state does not leak between tests.
/// </summary>
public sealed class TelemetryPipelineTests : IAsyncLifetime
{
    private ApiFactory _factory = null!;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private async Task ProcessAsync(string deviceId, double cpu = 30, double temp = 45, double latency = 60)
    {
        using var scope = _factory.Services.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<TelemetryProcessor>();
        await processor.ProcessAsync(new TelemetrySample(deviceId, _factory.Clock.UtcNow, cpu, temp, latency), CancellationToken.None);
    }

    private async Task<int> SweepAsync(int offlineAfterSeconds = 15)
    {
        using var scope = _factory.Services.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<TelemetryProcessor>();
        return await processor.MarkSilentDevicesOfflineAsync(TimeSpan.FromSeconds(offlineAfterSeconds), CancellationToken.None);
    }

    [Fact]
    public async Task Seeded_devices_start_offline_with_no_alerts()
    {
        using var client = await _factory.ClientAsAsync("viewer");

        var devices = await client.GetJsonAsync<List<DeviceDto>>("/api/devices");
        var summary = await client.GetJsonAsync<SummaryDto>("/api/summary");

        Assert.NotNull(devices);
        Assert.Equal(8, devices.Count);
        Assert.All(devices, d => Assert.Equal(DeviceStatus.Offline, d.Status));
        Assert.Equal(new SummaryDto(8, 0, 0, 8, 0, 0, 0, 0), summary);
    }

    [Fact]
    public async Task Healthy_sample_brings_device_online_and_is_queryable()
    {
        await ProcessAsync("edge-01", cpu: 42.5, temp: 50, latency: 70);
        _factory.Clock.Advance(TimeSpan.FromSeconds(2));
        await ProcessAsync("edge-01", cpu: 44, temp: 51, latency: 72);

        using var client = await _factory.ClientAsAsync("viewer");
        var device = await client.GetJsonAsync<DeviceDto>("/api/devices/edge-01");
        var readings = await client.GetJsonAsync<List<ReadingDto>>("/api/devices/edge-01/readings?minutes=5");

        Assert.NotNull(device);
        Assert.Equal(DeviceStatus.Online, device.Status);
        Assert.Equal(44, device.CpuPercent);
        Assert.Equal(_factory.Clock.UtcNow, device.LastSeenAt);
        Assert.NotNull(readings);
        Assert.Equal([42.5, 44], readings.Select(r => r.CpuPercent));
        Assert.True(readings[0].Timestamp < readings[1].Timestamp, "readings should be oldest first");
    }

    [Fact]
    public async Task Spike_raises_alert_once_and_clears_when_condition_passes()
    {
        await ProcessAsync("plc-01", cpu: 97);
        _factory.Clock.Advance(TimeSpan.FromSeconds(2));
        await ProcessAsync("plc-01", cpu: 98);   // still critical: no duplicate
        _factory.Clock.Advance(TimeSpan.FromSeconds(2));
        await ProcessAsync("plc-01", cpu: 88);   // de-escalates to warning in place

        using var client = await _factory.ClientAsAsync("viewer");
        var open = await client.GetJsonAsync<List<AlertDto>>("/api/alerts");
        var device = await client.GetJsonAsync<DeviceDto>("/api/devices/plc-01");

        var alert = Assert.Single(open!);
        Assert.Equal(AlertRules.HighCpu, alert.Kind);
        Assert.Equal(AlertSeverity.Warning, alert.Severity);
        Assert.Equal(_factory.Clock.UtcNow.AddSeconds(-4), alert.RaisedAt);
        Assert.Equal(DeviceStatus.Degraded, device!.Status);

        _factory.Clock.Advance(TimeSpan.FromSeconds(2));
        await ProcessAsync("plc-01", cpu: 40);

        var afterOpen = await client.GetJsonAsync<List<AlertDto>>("/api/alerts");
        var all = await client.GetJsonAsync<List<AlertDto>>("/api/alerts?open=false");
        var deviceAfter = await client.GetJsonAsync<DeviceDto>("/api/devices/plc-01");

        Assert.Empty(afterOpen!);
        var resolved = Assert.Single(all!);
        Assert.Equal("system", resolved.ResolvedBy);
        Assert.Equal(_factory.Clock.UtcNow, resolved.ResolvedAt);
        Assert.Equal(DeviceStatus.Online, deviceAfter!.Status);
    }

    [Fact]
    public async Task Silent_device_goes_offline_and_recovers()
    {
        await ProcessAsync("cam-01", cpu: 90); // online but degraded with a warning
        _factory.Clock.Advance(TimeSpan.FromSeconds(20));

        var marked = await SweepAsync(offlineAfterSeconds: 15);
        var markedAgain = await SweepAsync(offlineAfterSeconds: 15);

        using var client = await _factory.ClientAsAsync("viewer");
        var open = await client.GetJsonAsync<List<AlertDto>>("/api/alerts?deviceId=cam-01");
        var device = await client.GetJsonAsync<DeviceDto>("/api/devices/cam-01");

        Assert.Equal(1, marked);
        Assert.Equal(0, markedAgain);
        Assert.Equal(DeviceStatus.Offline, device!.Status);
        var offline = Assert.Single(open!);
        Assert.Equal(AlertRules.Offline, offline.Kind);
        Assert.Equal(AlertSeverity.Critical, offline.Severity);

        // Device reports again: offline alert resolves, state returns to online.
        _factory.Clock.Advance(TimeSpan.FromSeconds(5));
        await ProcessAsync("cam-01", cpu: 35);

        var openAfter = await client.GetJsonAsync<List<AlertDto>>("/api/alerts?deviceId=cam-01");
        var deviceAfter = await client.GetJsonAsync<DeviceDto>("/api/devices/cam-01");
        Assert.Empty(openAfter!);
        Assert.Equal(DeviceStatus.Online, deviceAfter!.Status);
    }

    [Fact]
    public async Task Operator_can_acknowledge_and_resolve_and_summary_reflects_it()
    {
        await ProcessAsync("ups-01", temp: 85);
        using var op = await _factory.ClientAsAsync("operator");
        var alert = Assert.Single((await op.GetJsonAsync<List<AlertDto>>("/api/alerts"))!);

        var ackResponse = await op.PostAsync($"/api/alerts/{alert.Id}/acknowledge", null);
        var acked = await ackResponse.Content.ReadJsonAsync<AlertDto>();
        var summary = await op.GetJsonAsync<SummaryDto>("/api/summary");

        Assert.Equal(HttpStatusCode.OK, ackResponse.StatusCode);
        Assert.Equal("operator", acked!.AcknowledgedBy);
        Assert.Equal(_factory.Clock.UtcNow, acked.AcknowledgedAt);
        Assert.Equal(1, summary!.OpenCritical);
        Assert.Equal(0, summary.Unacknowledged);

        var resolveResponse = await op.PostAsync($"/api/alerts/{alert.Id}/resolve", null);
        var resolved = await resolveResponse.Content.ReadJsonAsync<AlertDto>();
        var ackAfterResolve = await op.PostAsync($"/api/alerts/{alert.Id}/acknowledge", null);
        var unknown = await op.PostAsync("/api/alerts/9999/acknowledge", null);

        Assert.Equal("operator", resolved!.ResolvedBy);
        Assert.Equal(HttpStatusCode.Conflict, ackAfterResolve.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(0, (await op.GetJsonAsync<SummaryDto>("/api/summary"))!.OpenAlerts);
    }

    [Fact]
    public async Task Connected_dashboard_receives_live_updates_over_signalr()
    {
        var token = await _factory.TokenForAsync("viewer");
        var deviceUpdates = new TaskCompletionSource<DeviceDto>();
        var alertsRaised = new TaskCompletionSource<AlertDto>();

        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "/hubs/telemetry"), o =>
            {
                o.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                o.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()))
            .Build();
        connection.On<DeviceDto>("DeviceUpdated", d => deviceUpdates.TrySetResult(d));
        connection.On<AlertDto>("AlertRaised", a => alertsRaised.TrySetResult(a));
        await connection.StartAsync();

        await ProcessAsync("hvac-01", latency: 650);

        var device = await deviceUpdates.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var alert = await alertsRaised.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("hvac-01", device.Id);
        Assert.Equal(DeviceStatus.Degraded, device.Status);
        Assert.Equal(AlertRules.HighLatency, alert.Kind);
    }

    [Fact]
    public async Task Hub_rejects_anonymous_connections()
    {
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "/hubs/telemetry"), o => o.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler())
            .Build();

        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
    }
}
