using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using PulseBoard.Api.Contracts;

namespace PulseBoard.Api.Hubs;

/// <summary>Strongly-typed client surface so server code cannot misspell a client method.</summary>
public interface ITelemetryClient
{
    Task DeviceUpdated(DeviceDto device);
    Task ReadingReceived(string deviceId, ReadingDto reading);
    Task AlertRaised(AlertDto alert);
    Task AlertUpdated(AlertDto alert);
}

/// <summary>
/// Server-to-client push only. Clients authenticate with the same JWT as the REST API
/// (passed as <c>access_token</c> on the websocket query string).
/// </summary>
[Authorize]
public sealed class TelemetryHub : Hub<ITelemetryClient>
{
    public const string Path = "/hubs/telemetry";
}
