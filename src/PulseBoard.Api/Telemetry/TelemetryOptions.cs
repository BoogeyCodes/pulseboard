namespace PulseBoard.Api.Telemetry;

public sealed class TelemetryOptions
{
    public const string SectionName = "Telemetry";

    /// <summary>Run the built-in simulator that generates samples for the demo devices.</summary>
    public bool SimulatorEnabled { get; set; } = true;

    /// <summary>How often the simulator emits one sample per device.</summary>
    public int SampleIntervalSeconds { get; set; } = 2;

    /// <summary>A device with no sample for this long is marked offline.</summary>
    public int OfflineAfterSeconds { get; set; } = 15;

    /// <summary>Readings older than this are purged periodically.</summary>
    public int RetentionMinutes { get; set; } = 120;
}
