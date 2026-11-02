namespace PulseBoard.Api.Domain;

public enum DeviceStatus
{
    Online,
    Degraded,
    Offline,
}

/// <summary>A monitored piece of equipment and its most recent known state.</summary>
public sealed class Device
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Site { get; set; } = string.Empty;
    public DeviceStatus Status { get; set; } = DeviceStatus.Offline;
    public DateTimeOffset? LastSeenAt { get; set; }
    public double? LastCpuPercent { get; set; }
    public double? LastTemperatureC { get; set; }
    public double? LastLatencyMs { get; set; }
}
