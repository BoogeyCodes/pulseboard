namespace PulseBoard.Api.Domain;

/// <summary>One telemetry sample from a device.</summary>
public sealed class Reading
{
    public long Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public double CpuPercent { get; set; }
    public double TemperatureC { get; set; }
    public double LatencyMs { get; set; }
}
