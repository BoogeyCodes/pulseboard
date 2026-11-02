using Dapper;
using Microsoft.EntityFrameworkCore;
using PulseBoard.Api.Contracts;

namespace PulseBoard.Api.Infrastructure;

/// <summary>
/// Read-side queries over telemetry. EF Core owns writes and the schema; the hot
/// time-series reads go through Dapper so the SQL (and its index usage) is explicit.
/// </summary>
public sealed class ReadingsRepository(AppDbContext db)
{
    public async Task<IReadOnlyList<ReadingDto>> GetRecentAsync(string deviceId, DateTimeOffset since, int limit, CancellationToken ct)
    {
        const string sql = """
            SELECT Timestamp, CpuPercent, TemperatureC, LatencyMs
            FROM Readings
            WHERE DeviceId = @deviceId AND Timestamp >= @since
            ORDER BY Timestamp DESC
            LIMIT @limit
            """;

        var connection = db.Database.GetDbConnection();
        var command = new CommandDefinition(sql, new { deviceId, since = ToSqliteText(since), limit }, cancellationToken: ct);
        var rows = await connection.QueryAsync<ReadingRow>(command);

        // Newest-first from SQL for the LIMIT, oldest-first for the caller so charts read left to right.
        return rows
            .Select(r => new ReadingDto(DateTimeOffset.Parse(r.Timestamp), r.CpuPercent, r.TemperatureC, r.LatencyMs))
            .OrderBy(r => r.Timestamp)
            .ToList();
    }

    public async Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        var command = new CommandDefinition(
            "DELETE FROM Readings WHERE Timestamp < @cutoff",
            new { cutoff = ToSqliteText(cutoff) },
            cancellationToken: ct);
        return await connection.ExecuteAsync(command);
    }

    /// <summary>
    /// EF Core's SQLite provider stores DateTimeOffset as text in this exact layout. Parameters must use
    /// the same layout so the WHERE clause compares like with like (lexicographic order == chronological).
    /// </summary>
    private static string ToSqliteText(DateTimeOffset value) => value.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFFzzz");

    // Dapper maps the stored text back through this row type.
    private sealed record ReadingRow(string Timestamp, double CpuPercent, double TemperatureC, double LatencyMs);
}
