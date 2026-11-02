using System.Threading.Channels;

namespace PulseBoard.Api.Telemetry;

/// <summary>
/// Bounded in-process queue between telemetry producers and the single processor.
/// Producers never block the request path; if the processor falls behind, the oldest
/// samples are dropped rather than growing memory without bound.
/// </summary>
public sealed class TelemetryChannel
{
    private readonly Channel<TelemetrySample> _channel = Channel.CreateBounded<TelemetrySample>(new BoundedChannelOptions(1024)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true,
        SingleWriter = false,
    });

    public ChannelWriter<TelemetrySample> Writer => _channel.Writer;
    public ChannelReader<TelemetrySample> Reader => _channel.Reader;
}
