namespace PulseBoard.Api.Telemetry;

/// <summary>The message that flows through the telemetry channel from producers to the processor.</summary>
public sealed record TelemetrySample(
    string DeviceId,
    DateTimeOffset Timestamp,
    double CpuPercent,
    double TemperatureC,
    double LatencyMs);
