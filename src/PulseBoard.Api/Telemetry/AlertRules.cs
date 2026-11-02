using PulseBoard.Api.Domain;

namespace PulseBoard.Api.Telemetry;

public sealed record AlertCandidate(string Kind, AlertSeverity Severity, string Message);

/// <summary>
/// Pure threshold rules. Given a sample, returns the alert conditions that currently hold.
/// Kinds not returned are considered cleared, which lets the processor auto-resolve them.
/// </summary>
public static class AlertRules
{
    public const string HighCpu = "high-cpu";
    public const string Overheating = "overheating";
    public const string HighLatency = "high-latency";
    public const string Offline = "offline";

    public const double CpuWarningPercent = 85;
    public const double CpuCriticalPercent = 95;
    public const double TemperatureCriticalC = 80;
    public const double LatencyWarningMs = 400;

    /// <summary>Rule kinds that are derived from samples (as opposed to the heartbeat monitor).</summary>
    public static readonly IReadOnlyList<string> SampleKinds = [HighCpu, Overheating, HighLatency];

    public static IReadOnlyList<AlertCandidate> Evaluate(TelemetrySample s)
    {
        var result = new List<AlertCandidate>(3);

        if (s.CpuPercent >= CpuCriticalPercent)
        {
            result.Add(new(HighCpu, AlertSeverity.Critical, $"CPU at {s.CpuPercent:F0}% (critical threshold {CpuCriticalPercent}%)"));
        }
        else if (s.CpuPercent >= CpuWarningPercent)
        {
            result.Add(new(HighCpu, AlertSeverity.Warning, $"CPU at {s.CpuPercent:F0}% (warning threshold {CpuWarningPercent}%)"));
        }

        if (s.TemperatureC >= TemperatureCriticalC)
        {
            result.Add(new(Overheating, AlertSeverity.Critical, $"Temperature {s.TemperatureC:F1}°C exceeds {TemperatureCriticalC}°C"));
        }

        if (s.LatencyMs >= LatencyWarningMs)
        {
            result.Add(new(HighLatency, AlertSeverity.Warning, $"Latency {s.LatencyMs:F0} ms exceeds {LatencyWarningMs} ms"));
        }

        return result;
    }

    /// <summary>A device is degraded while any sample-derived condition holds, otherwise online.</summary>
    public static DeviceStatus StatusFor(IReadOnlyList<AlertCandidate> active) =>
        active.Count == 0 ? DeviceStatus.Online : DeviceStatus.Degraded;
}
