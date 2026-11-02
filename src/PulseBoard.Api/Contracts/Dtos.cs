using PulseBoard.Api.Domain;

namespace PulseBoard.Api.Contracts;

public sealed record LoginRequest(string Username, string Password);

public sealed record LoginResponse(string Token, string Username, string Role, DateTimeOffset ExpiresAt);

public sealed record DeviceDto(
    string Id,
    string Name,
    string Site,
    DeviceStatus Status,
    DateTimeOffset? LastSeenAt,
    double? CpuPercent,
    double? TemperatureC,
    double? LatencyMs)
{
    public static DeviceDto From(Device d) =>
        new(d.Id, d.Name, d.Site, d.Status, d.LastSeenAt, d.LastCpuPercent, d.LastTemperatureC, d.LastLatencyMs);
}

public sealed record ReadingDto(DateTimeOffset Timestamp, double CpuPercent, double TemperatureC, double LatencyMs);

public sealed record AlertDto(
    int Id,
    string DeviceId,
    string Kind,
    AlertSeverity Severity,
    string Message,
    DateTimeOffset RaisedAt,
    DateTimeOffset? AcknowledgedAt,
    string? AcknowledgedBy,
    DateTimeOffset? ResolvedAt,
    string? ResolvedBy)
{
    public static AlertDto From(Alert a) => new(
        a.Id, a.DeviceId, a.Kind, a.Severity, a.Message, a.RaisedAt,
        a.AcknowledgedAt, a.AcknowledgedBy, a.ResolvedAt, a.ResolvedBy);
}

public sealed record SummaryDto(
    int Devices,
    int Online,
    int Degraded,
    int Offline,
    int OpenAlerts,
    int OpenCritical,
    int OpenWarning,
    int Unacknowledged);
