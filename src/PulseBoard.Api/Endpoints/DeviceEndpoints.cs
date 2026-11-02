using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using PulseBoard.Api.Auth;
using PulseBoard.Api.Contracts;
using PulseBoard.Api.Domain;
using PulseBoard.Api.Infrastructure;

namespace PulseBoard.Api.Endpoints;

public static class DeviceEndpoints
{
    public static IEndpointRouteBuilder MapDeviceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/devices").WithTags("Devices").RequireAuthorization(Policies.CanView);

        group.MapGet("/", async (AppDbContext db, CancellationToken ct) =>
            TypedResults.Ok(await db.Devices.AsNoTracking().OrderBy(d => d.Site).ThenBy(d => d.Name).Select(d => DeviceDto.From(d)).ToListAsync(ct)))
        .WithName("ListDevices")
        .WithSummary("All devices with their last-known state");

        group.MapGet("/{id}", async Task<Results<Ok<DeviceDto>, NotFound>> (string id, AppDbContext db, CancellationToken ct) =>
        {
            var device = await db.Devices.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id, ct);
            return device is null ? TypedResults.NotFound() : TypedResults.Ok(DeviceDto.From(device));
        })
        .WithName("GetDevice");

        group.MapGet("/{id}/readings", async Task<Results<Ok<IReadOnlyList<ReadingDto>>, NotFound>> (
            string id, int? minutes, int? limit, AppDbContext db, ReadingsRepository readings, IClock clock, CancellationToken ct) =>
        {
            if (!await db.Devices.AnyAsync(d => d.Id == id, ct))
            {
                return TypedResults.NotFound();
            }

            var window = TimeSpan.FromMinutes(Math.Clamp(minutes ?? 15, 1, 24 * 60));
            var rows = await readings.GetRecentAsync(id, clock.UtcNow - window, Math.Clamp(limit ?? 500, 1, 5000), ct);
            return TypedResults.Ok(rows);
        })
        .WithName("GetDeviceReadings")
        .WithSummary("Recent telemetry for a device, oldest first");

        app.MapGet("/api/summary", async (AppDbContext db, CancellationToken ct) =>
        {
            var devices = await db.Devices.AsNoTracking().GroupBy(d => d.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
            var open = await db.Alerts.AsNoTracking().Where(a => a.ResolvedAt == null).ToListAsync(ct);

            int Of(DeviceStatus s) => devices.SingleOrDefault(x => x.Key == s)?.Count ?? 0;

            return TypedResults.Ok(new SummaryDto(
                Devices: devices.Sum(x => x.Count),
                Online: Of(DeviceStatus.Online),
                Degraded: Of(DeviceStatus.Degraded),
                Offline: Of(DeviceStatus.Offline),
                OpenAlerts: open.Count,
                OpenCritical: open.Count(a => a.Severity == AlertSeverity.Critical),
                OpenWarning: open.Count(a => a.Severity == AlertSeverity.Warning),
                Unacknowledged: open.Count(a => a.AcknowledgedAt == null)));
        })
        .WithName("GetSummary")
        .WithTags("Devices")
        .RequireAuthorization(Policies.CanView);

        return app;
    }
}
