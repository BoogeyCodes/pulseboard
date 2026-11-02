namespace PulseBoard.Api.Domain;

public enum AlertSeverity
{
    Info,
    Warning,
    Critical,
}

/// <summary>
/// A condition detected on a device. An alert is <em>open</em> until it is resolved,
/// either automatically when the condition clears or manually by an operator.
/// Acknowledging records that a person has seen it; it does not close it.
/// </summary>
public sealed class Alert
{
    public int Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>Stable identifier of the rule that raised the alert, e.g. <c>high-cpu</c>.</summary>
    public string Kind { get; set; } = string.Empty;

    public AlertSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset RaisedAt { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }

    public bool IsOpen => ResolvedAt is null;
}
