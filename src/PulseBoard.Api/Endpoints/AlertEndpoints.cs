using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PulseBoard.Api.Auth;
using PulseBoard.Api.Contracts;
using PulseBoard.Api.Hubs;
using PulseBoard.Api.Infrastructure;

namespace PulseBoard.Api.Endpoints;

public static class AlertEndpoints
{
    public static IEndpointRouteBuilder MapAlertEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/alerts").WithTags("Alerts");

        group.MapGet("/", async (bool? open, string? deviceId, int? limit, AppDbContext db, CancellationToken ct) =>
        {
            var query = db.Alerts.AsNoTracking();
            if (open ?? true)
            {
                query = query.Where(a => a.ResolvedAt == null);
            }
            if (!string.IsNullOrEmpty(deviceId))
            {
                query = query.Where(a => a.DeviceId == deviceId);
            }

            // Ids are monotonic, so this is "newest first" without ordering by DateTimeOffset (unsupported in SQLite).
            var rows = await query
                .OrderByDescending(a => a.Id)
                .Take(Math.Clamp(limit ?? 100, 1, 1000))
                .Select(a => AlertDto.From(a))
                .ToListAsync(ct);
            return TypedResults.Ok(rows);
        })
        .WithName("ListAlerts")
        .WithSummary("Alerts, open only by default, newest first")
        .RequireAuthorization(Policies.CanView);

        group.MapPost("/{id:int}/acknowledge", async Task<Results<Ok<AlertDto>, NotFound, Conflict<string>>> (
            int id, HttpContext http, AppDbContext db, IHubContext<TelemetryHub, ITelemetryClient> hub, IClock clock, CancellationToken ct) =>
        {
            var alert = await db.Alerts.SingleOrDefaultAsync(a => a.Id == id, ct);
            if (alert is null)
            {
                return TypedResults.NotFound();
            }
            if (alert.ResolvedAt is not null)
            {
                return TypedResults.Conflict("Alert is already resolved.");
            }
            if (alert.AcknowledgedAt is null)
            {
                alert.AcknowledgedAt = clock.UtcNow;
                alert.AcknowledgedBy = http.User.Identity?.Name;
                await db.SaveChangesAsync(ct);
                await hub.Clients.All.AlertUpdated(AlertDto.From(alert));
            }
            return TypedResults.Ok(AlertDto.From(alert));
        })
        .WithName("AcknowledgeAlert")
        .WithSummary("Record that an operator has seen the alert")
        .RequireAuthorization(Policies.CanOperate);

        group.MapPost("/{id:int}/resolve", async Task<Results<Ok<AlertDto>, NotFound>> (
            int id, HttpContext http, AppDbContext db, IHubContext<TelemetryHub, ITelemetryClient> hub, IClock clock, CancellationToken ct) =>
        {
            var alert = await db.Alerts.SingleOrDefaultAsync(a => a.Id == id, ct);
            if (alert is null)
            {
                return TypedResults.NotFound();
            }
            if (alert.ResolvedAt is null)
            {
                alert.ResolvedAt = clock.UtcNow;
                alert.ResolvedBy = http.User.Identity?.Name;
                alert.AcknowledgedAt ??= alert.ResolvedAt;
                alert.AcknowledgedBy ??= alert.ResolvedBy;
                await db.SaveChangesAsync(ct);
                await hub.Clients.All.AlertUpdated(AlertDto.From(alert));
            }
            return TypedResults.Ok(AlertDto.From(alert));
        })
        .WithName("ResolveAlert")
        .WithSummary("Close an alert manually")
        .RequireAuthorization(Policies.CanOperate);

        return app;
    }
}
