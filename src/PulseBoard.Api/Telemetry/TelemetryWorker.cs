using Microsoft.Extensions.Options;

namespace PulseBoard.Api.Telemetry;

/// <summary>
/// The single consumer of the telemetry channel. Also runs the heartbeat sweep and
/// retention purge on a timer so there is exactly one writer touching device state.
/// </summary>
public sealed class TelemetryWorker(
    TelemetryChannel channel,
    IServiceScopeFactory scopes,
    IOptions<TelemetryOptions> options,
    ILogger<TelemetryWorker> logger) : BackgroundService
{
    private readonly TelemetryOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Telemetry worker started");
        var sweeper = SweepAsync(stoppingToken);

        try
        {
            await foreach (var sample in channel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    var processor = scope.ServiceProvider.GetRequiredService<TelemetryProcessor>();
                    await processor.ProcessAsync(sample, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // One bad sample must not take the pipeline down.
                    logger.LogError(ex, "Failed to process sample for {DeviceId}", sample.DeviceId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }

        await sweeper;
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        var lastPurge = DateTimeOffset.MinValue;

        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                using var scope = scopes.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<TelemetryProcessor>();
                await processor.MarkSilentDevicesOfflineAsync(TimeSpan.FromSeconds(_options.OfflineAfterSeconds), ct);

                if (DateTimeOffset.UtcNow - lastPurge > TimeSpan.FromMinutes(5))
                {
                    var readings = scope.ServiceProvider.GetRequiredService<Infrastructure.ReadingsRepository>();
                    var purged = await readings.DeleteOlderThanAsync(DateTimeOffset.UtcNow.AddMinutes(-_options.RetentionMinutes), ct);
                    if (purged > 0)
                    {
                        logger.LogInformation("Purged {Count} readings older than {Minutes} minutes", purged, _options.RetentionMinutes);
                    }
                    lastPurge = DateTimeOffset.UtcNow;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }
}
