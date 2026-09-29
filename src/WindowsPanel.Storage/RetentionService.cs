using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;

namespace WindowsPanel.Storage;

/// <summary>
/// 归档 + 保留期清理（README §5.3）：
///   - 每分钟 1 次把 metric_raw 聚合到 metric_1m
///   - 每小时 1 次把 metric_1m 聚合到 metric_1h
///   - 按保留窗口删除过期点
/// </summary>
public sealed class RetentionService : BackgroundService
{
    private readonly string dbPath;
    private readonly TimeSpan rawRetention;
    private readonly TimeSpan oneMinuteRetention;
    private readonly TimeSpan oneHourRetention;

    public RetentionService(string dbPath, TimeSpan rawRetention, TimeSpan oneMinuteRetention, TimeSpan oneHourRetention)
    {
        this.dbPath = dbPath;
        this.rawRetention = rawRetention;
        this.oneMinuteRetention = oneMinuteRetention;
        this.oneHourRetention = oneHourRetention;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await TickAsync(ct); }
            catch { /* 单次失败不致命，下一轮再试 */ }
            await Task.Delay(TimeSpan.FromSeconds(60), ct);
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        const long oneMinBucketMs  = 60_000L;
        const long oneHourBucketMs = 3_600_000L;

        await using var conn = new SqliteConnection($"Data Source={dbPath};");
        await conn.OpenAsync(ct);

        // 聚合上一分钟 raw → 1m（源列名 value）
        var lastMinTs = (nowMs / oneMinBucketMs - 1) * oneMinBucketMs;
        await AggregateAsync(conn, "metric_raw", "metric_1m", "metric", "value", lastMinTs, oneMinBucketMs, ct);

        // 聚合上一小时 1m → 1h（源列名 avg_v）
        var lastHourTs = (nowMs / oneHourBucketMs - 1) * oneHourBucketMs;
        await AggregateAsync(conn, "metric_1m", "metric_1h", "metric", "avg_v", lastHourTs, oneHourBucketMs, ct);

        // 清理过期点
        await DeleteBeforeAsync(conn, "metric_raw", nowMs - (long)rawRetention.TotalMilliseconds,        ct);
        await DeleteBeforeAsync(conn, "metric_1m",  nowMs - (long)oneMinuteRetention.TotalMilliseconds, ct);
        await DeleteBeforeAsync(conn, "metric_1h",  nowMs - (long)oneHourRetention.TotalMilliseconds,    ct);
    }

    private static async Task AggregateAsync(SqliteConnection conn, string src, string dst, string keyCol, string srcCol, long bucketStart, long bucketSize, CancellationToken ct)
    {
        var bucketEnd = bucketStart + bucketSize;
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            INSERT OR REPLACE INTO {dst} ({keyCol}, ts, avg_v, max_v, min_v)
            SELECT {keyCol}, $bucket AS ts, AVG({srcCol}), MAX({srcCol}), MIN({srcCol})
            FROM {src}
            WHERE ts >= $start AND ts < $end
            GROUP BY {keyCol}
            """;
        cmd.Parameters.AddWithValue("$bucket", bucketStart);
        cmd.Parameters.AddWithValue("$start",  bucketStart);
        cmd.Parameters.AddWithValue("$end",    bucketEnd);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task DeleteBeforeAsync(SqliteConnection conn, string table, long cutoffMs, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM {table} WHERE ts < $cutoff";
        cmd.Parameters.AddWithValue("$cutoff", cutoffMs);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}