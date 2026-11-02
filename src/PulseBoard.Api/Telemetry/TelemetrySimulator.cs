using Microsoft.Extensions.Options;
using PulseBoard.Api.Infrastructure;

namespace PulseBoard.Api.Telemetry;

/// <summary>
/// Stand-in for real devices. Each demo device follows a random walk around a baseline,
/// with occasional load spikes and the odd outage so the dashboard has something to show.
/// Replace with an MQTT or HTTP ingest producer in a real deployment; the channel contract is the same.
/// </summary>
public sealed class TelemetrySimulator(
    TelemetryChannel channel,
    IOptions<TelemetryOptions> options,
    IClock clock,
    ILogger<TelemetrySimulator> logger) : BackgroundService
{
    private readonly TelemetryOptions _options = options.Value;
    private readonly Random _random = new();
    private readonly Dictionary<string, DeviceModel> _models = DbSeeder.DemoDevices.ToDictionary(d => d.Id, _ => new DeviceModel());

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Telemetry simulator started ({Count} devices, every {Seconds}s)", _models.Count, _options.SampleIntervalSeconds);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.SampleIntervalSeconds));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var now = clock.UtcNow;
                foreach (var (deviceId, model) in _models)
                {
                    if (!model.Step(_random))
                    {
                        continue; // simulated outage: no sample this tick
                    }

                    channel.Writer.TryWrite(new TelemetrySample(deviceId, now, model.Cpu, model.Temp, model.Latency));
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }

    private sealed class DeviceModel
    {
        public double Cpu = 30;
        public double Temp = 45;
        public double Latency = 60;

        private int _spikeTicks;
        private int _outageTicks;

        /// <summary>Advances the model one tick. Returns false when the device is "offline" this tick.</summary>
        public bool Step(Random rng)
        {
            if (_outageTicks > 0)
            {
                _outageTicks--;
                return false;
            }

            // Roughly one outage per device every ~5 minutes at a 2 s interval, lasting 20-40 s.
            if (rng.NextDouble() < 1.0 / 150)
            {
                _outageTicks = rng.Next(10, 20);
                return false;
            }

            // Roughly one load spike every ~2 minutes, lasting 10-30 s.
            if (_spikeTicks == 0 && rng.NextDouble() < 1.0 / 60)
            {
                _spikeTicks = rng.Next(5, 15);
            }

            var (cpuTarget, tempTarget, latencyTarget) = _spikeTicks > 0
                ? (93.0, 78.0, 300.0)
                : (30.0, 45.0, 60.0);
            if (_spikeTicks > 0)
            {
                _spikeTicks--;
            }

            Cpu = Drift(Cpu, cpuTarget, 0.35, 6, rng, 0, 100);
            Temp = Drift(Temp, tempTarget, 0.25, 1.5, rng, 10, 110);
            Latency = Drift(Latency, latencyTarget, 0.4, 40, rng, 1, 2000);
            return true;
        }

        private static double Drift(double current, double target, double pull, double noise, Random rng, double min, double max)
        {
            var next = current + (target - current) * pull + (rng.NextDouble() * 2 - 1) * noise;
            return Math.Clamp(next, min, max);
        }
    }
}
