using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PulseBoard.Api.Contracts;
using PulseBoard.Api.Domain;
using PulseBoard.Api.Hubs;
using PulseBoard.Api.Infrastructure;

namespace PulseBoard.Api.Telemetry;

/// <summary>
/// Applies one telemetry sample: persists it, updates the device's last-known state,
/// evaluates alert rules (raising new alerts and auto-resolving cleared ones) and
/// broadcasts the resulting changes to connected dashboards.
/// Scoped so each sample gets a fresh DbContext.
/// </summary>
public sealed class TelemetryProcessor(
    AppDbContext db,
    IHubContext<TelemetryHub, ITelemetryClient> hub,
    IClock clock,
    ILogger<TelemetryProcessor> logger)
{
    public async Task ProcessAsync(TelemetrySample sample, CancellationToken ct)
    {
        var device = await db.Devices.SingleOrDefaultAsync(d => d.Id == sample.DeviceId, ct);
        if (device is null)
        {
            logger.LogWarning("Dropping sample for unknown device {DeviceId}", sample.DeviceId);
            return;
        }

        db.Readings.Add(new Reading
        {
            DeviceId = sample.DeviceId,
            Timestamp = sample.Timestamp,
            CpuPercent = sample.CpuPercent,
            TemperatureC = sample.TemperatureC,
            LatencyMs = sample.LatencyMs,
        });

        var active = AlertRules.Evaluate(sample);
        var previousStatus = device.Status;

        device.LastSeenAt = sample.Timestamp;
        device.LastCpuPercent = sample.CpuPercent;
        device.LastTemperatureC = sample.TemperatureC;
        device.LastLatencyMs = sample.LatencyMs;
        device.Status = AlertRules.StatusFor(active);

        var changedAlerts = await ReconcileAlertsAsync(device, active, ct);

        // A device that was offline and is now reporting again: close the offline alert.
        if (previousStatus == DeviceStatus.Offline)
        {
            changedAlerts.AddRange(await ResolveOpenAsync(device.Id, AlertRules.Offline, "system", ct));
        }

        await db.SaveChangesAsync(ct);

        await hub.Clients.All.ReadingReceived(device.Id, new ReadingDto(sample.Timestamp, sample.CpuPercent, sample.TemperatureC, sample.LatencyMs));
        await hub.Clients.All.DeviceUpdated(DeviceDto.From(device));
        foreach (var (alert, isNew) in changedAlerts)
        {
            if (isNew)
            {
                await hub.Clients.All.AlertRaised(AlertDto.From(alert));
            }
            else
            {
                await hub.Clients.All.AlertUpdated(AlertDto.From(alert));
            }
        }
    }

    /// <summary>Marks devices that have gone quiet as offline and raises a critical alert once per outage.</summary>
    public async Task<int> MarkSilentDevicesOfflineAsync(TimeSpan offlineAfter, CancellationToken ct)
    {
        var cutoff = clock.UtcNow - offlineAfter;
        // SQLite cannot compare DateTimeOffset columns server-side; the device table is small, so filter in memory.
        var candidates = await db.Devices
            .Where(d => d.Status != DeviceStatus.Offline && d.LastSeenAt != null)
            .ToListAsync(ct);
        var silent = candidates.Where(d => d.LastSeenAt < cutoff).ToList();

        foreach (var device in silent)
        {
            device.Status = DeviceStatus.Offline;
            var changed = new List<(Alert, bool)>();

            // Any sample-derived alerts are meaningless once the device is silent.
            foreach (var kind in AlertRules.SampleKinds)
            {
                changed.AddRange(await ResolveOpenAsync(device.Id, kind, "system", ct));
            }

            var alreadyOpen = await db.Alerts.AnyAsync(a => a.DeviceId == device.Id && a.Kind == AlertRules.Offline && a.ResolvedAt == null, ct);
            if (!alreadyOpen)
            {
                var alert = new Alert
                {
                    DeviceId = device.Id,
                    Kind = AlertRules.Offline,
                    Severity = AlertSeverity.Critical,
                    Message = $"No telemetry for {offlineAfter.TotalSeconds:F0}s (last seen {device.LastSeenAt:HH:mm:ss} UTC)",
                    RaisedAt = clock.UtcNow,
                };
                db.Alerts.Add(alert);
                changed.Add((alert, true));
                logger.LogWarning("Device {DeviceId} marked offline", device.Id);
            }

            await db.SaveChangesAsync(ct);
            await hub.Clients.All.DeviceUpdated(DeviceDto.From(device));
            foreach (var (alert, isNew) in changed)
            {
                if (isNew)
                {
                    await hub.Clients.All.AlertRaised(AlertDto.From(alert));
                }
                else
                {
                    await hub.Clients.All.AlertUpdated(AlertDto.From(alert));
                }
            }
        }

        return silent.Count;
    }

    private async Task<List<(Alert Alert, bool IsNew)>> ReconcileAlertsAsync(Device device, IReadOnlyList<AlertCandidate> active, CancellationToken ct)
    {
        var changed = new List<(Alert, bool)>();
        var open = await db.Alerts
            .Where(a => a.DeviceId == device.Id && a.ResolvedAt == null && AlertRules.SampleKinds.Contains(a.Kind))
            .ToListAsync(ct);

        foreach (var candidate in active)
        {
            var existing = open.SingleOrDefault(a => a.Kind == candidate.Kind);
            if (existing is null)
            {
                var alert = new Alert
                {
                    DeviceId = device.Id,
                    Kind = candidate.Kind,
                    Severity = candidate.Severity,
                    Message = candidate.Message,
                    RaisedAt = clock.UtcNow,
                };
                db.Alerts.Add(alert);
                changed.Add((alert, true));
                logger.LogInformation("Alert {Kind} raised for {DeviceId}: {Message}", alert.Kind, device.Id, alert.Message);
            }
            else if (existing.Severity != candidate.Severity || existing.Message != candidate.Message)
            {
                // Escalate or de-escalate in place; keep the original RaisedAt so duration is honest.
                existing.Severity = candidate.Severity;
                existing.Message = candidate.Message;
                changed.Add((existing, false));
            }
        }

        var activeKinds = active.Select(a => a.Kind).ToHashSet();
        foreach (var cleared in open.Where(a => !activeKinds.Contains(a.Kind)))
        {
            cleared.ResolvedAt = clock.UtcNow;
            cleared.ResolvedBy = "system";
            changed.Add((cleared, false));
            logger.LogInformation("Alert {Kind} auto-resolved for {DeviceId}", cleared.Kind, device.Id);
        }

        return changed;
    }

    private async Task<List<(Alert, bool)>> ResolveOpenAsync(string deviceId, string kind, string by, CancellationToken ct)
    {
        var open = await db.Alerts.Where(a => a.DeviceId == deviceId && a.Kind == kind && a.ResolvedAt == null).ToListAsync(ct);
        foreach (var alert in open)
        {
            alert.ResolvedAt = clock.UtcNow;
            alert.ResolvedBy = by;
        }
        return open.Select(a => (a, false)).ToList();
    }
}
