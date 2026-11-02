using PulseBoard.Api.Domain;
using PulseBoard.Api.Telemetry;

namespace PulseBoard.Api.Tests;

public sealed class AlertRulesTests
{
    private static TelemetrySample Sample(double cpu = 30, double temp = 45, double latency = 60) =>
        new("edge-01", DateTimeOffset.UnixEpoch, cpu, temp, latency);

    [Fact]
    public void Healthy_sample_raises_nothing_and_is_online()
    {
        var active = AlertRules.Evaluate(Sample());

        Assert.Empty(active);
        Assert.Equal(DeviceStatus.Online, AlertRules.StatusFor(active));
    }

    [Theory]
    [InlineData(85, AlertSeverity.Warning)]
    [InlineData(94.9, AlertSeverity.Warning)]
    [InlineData(95, AlertSeverity.Critical)]
    [InlineData(100, AlertSeverity.Critical)]
    public void Cpu_thresholds_map_to_severity(double cpu, AlertSeverity expected)
    {
        var active = AlertRules.Evaluate(Sample(cpu: cpu));

        var alert = Assert.Single(active);
        Assert.Equal(AlertRules.HighCpu, alert.Kind);
        Assert.Equal(expected, alert.Severity);
        Assert.Equal(DeviceStatus.Degraded, AlertRules.StatusFor(active));
    }

    [Fact]
    public void Just_below_cpu_warning_is_clean()
    {
        Assert.Empty(AlertRules.Evaluate(Sample(cpu: 84.9)));
    }

    [Fact]
    public void Overheating_is_always_critical()
    {
        var alert = Assert.Single(AlertRules.Evaluate(Sample(temp: 80)));

        Assert.Equal(AlertRules.Overheating, alert.Kind);
        Assert.Equal(AlertSeverity.Critical, alert.Severity);
    }

    [Fact]
    public void Multiple_conditions_are_all_reported()
    {
        var active = AlertRules.Evaluate(Sample(cpu: 99, temp: 85, latency: 900));

        Assert.Equal([AlertRules.HighCpu, AlertRules.Overheating, AlertRules.HighLatency], active.Select(a => a.Kind));
    }
}
