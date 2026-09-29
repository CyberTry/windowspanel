using Microsoft.Data.Sqlite;

namespace WindowsPanel.Storage;

/// <summary>三级粒度（raw / 1m / 1h）的写入与查询接口。</summary>
public sealed class MetricStore
{
    private readonly string connStr;
    public MetricStore(string dbPath) => connStr = $"Data Source={dbPath};";

    public void InsertRaw(string metric, long ts, double value)
    {
        using var conn = new SqliteConnection(connStr);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO metric_raw (metric,ts,value) VALUES ($m,$t,$v)";
        cmd.Parameters.AddWithValue("$m", metric);
        cmd.Parameters.AddWithValue("$t", ts);
        cmd.Parameters.AddWithValue("$v", value);
        cmd.ExecuteNonQuery();
    }

    public async Task InsertRawBatchAsync(IReadOnlyDictionary<string, double> metrics, long ts)
    {
        await using var conn = new SqliteConnection(connStr);
        conn.Open();
        await using var tx = conn.BeginTransaction();
        foreach (var (m, v) in metrics)
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = (SqliteTransaction)tx;
            cmd.CommandText = "INSERT OR REPLACE INTO metric_raw (metric,ts,value) VALUES ($m,$t,$vv)";
            cmd.Parameters.AddWithValue("$m", m);
            cmd.Parameters.AddWithValue("$t", ts);
            cmd.Parameters.AddWithValue("$vv", v);
            await cmd.ExecuteNonQueryAsync();
        }
        tx.Commit();
    }

    public IReadOnlyList<(long Ts, double Value)> QueryRaw(string metric, long from, long to, int limit = 5000)
    {
        var list = new List<(long, double)>();
        using var conn = new SqliteConnection(connStr);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT ts,value FROM metric_raw WHERE metric=$m AND ts>=$f AND ts<$t ORDER BY ts LIMIT $l";
        cmd.Parameters.AddWithValue("$m", metric);
        cmd.Parameters.AddWithValue("$f", from);
        cmd.Parameters.AddWithValue("$t", to);
        cmd.Parameters.AddWithValue("$l", limit);
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add((r.GetInt64(0), r.GetDouble(1)));
        return list;
    }

    public IReadOnlyList<(long Ts, double Avg, double Max, double Min)> Query1m(string metric, long from, long to, int limit = 5000)
    {
        var list = new List<(long, double, double, double)>();
        using var conn = new SqliteConnection(connStr);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT ts,avg_v,max_v,min_v FROM metric_1m WHERE metric=$m AND ts>=$f AND ts<$t ORDER BY ts LIMIT $l";
        cmd.Parameters.AddWithValue("$m", metric);
        cmd.Parameters.AddWithValue("$f", from);
        cmd.Parameters.AddWithValue("$t", to);
        cmd.Parameters.AddWithValue("$l", limit);
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add((r.GetInt64(0), r.GetDouble(1), r.GetDouble(2), r.GetDouble(3)));
        return list;
    }

    public IReadOnlyList<(long Ts, double Avg, double Max, double Min)> Query1h(string metric, long from, long to, int limit = 5000)
    {
        var list = new List<(long, double, double, double)>();
        using var conn = new SqliteConnection(connStr);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT ts,avg_v,max_v,min_v FROM metric_1h WHERE metric=$m AND ts>=$f AND ts<$t ORDER BY ts LIMIT $l";
        cmd.Parameters.AddWithValue("$m", metric);
        cmd.Parameters.AddWithValue("$f", from);
        cmd.Parameters.AddWithValue("$t", to);
        cmd.Parameters.AddWithValue("$l", limit);
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add((r.GetInt64(0), r.GetDouble(1), r.GetDouble(2), r.GetDouble(3)));
        return list;
    }
}