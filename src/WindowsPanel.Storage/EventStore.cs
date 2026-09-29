using Microsoft.Data.Sqlite;

namespace WindowsPanel.Storage;

public enum EventLevel    { Info, Warn, Error }
public enum EventCategory { Threshold, Service, Audit }

public sealed record EventEntry(
    long           Id,
    long           Ts,
    EventLevel     Level,
    EventCategory  Category,
    string?        Metric,
    double?        Value,
    string         Message
);

/// <summary>事件日志（阈值 / 服务 / 审计）的写入与查询。</summary>
public sealed class EventStore
{
    private readonly string connStr;
    public EventStore(string dbPath) => connStr = $"Data Source={dbPath};";

    public void Insert(long ts, EventLevel level, EventCategory cat, string message, string? metric = null, double? value = null)
    {
        using var conn = new SqliteConnection(connStr);
        conn.Open();
        using var c = conn.CreateCommand();
        c.CommandText = "INSERT INTO events (ts,level,category,metric,value,message) VALUES ($ts,$lv,$cat,$m,$v,$msg)";
        c.Parameters.AddWithValue("$ts", ts);
        c.Parameters.AddWithValue("$lv", level.ToString().ToLowerInvariant());
        c.Parameters.AddWithValue("$cat", cat.ToString().ToLowerInvariant());
        c.Parameters.AddWithValue("$m", (object?)metric ?? DBNull.Value);
        c.Parameters.AddWithValue("$v", (object?)value ?? DBNull.Value);
        c.Parameters.AddWithValue("$msg", message);
        c.ExecuteNonQuery();
    }

    public IReadOnlyList<EventEntry> Query(EventLevel? level = null, int limit = 200, long? from = null, long? to = null)
    {
        var list = new List<EventEntry>();
        using var conn = new SqliteConnection(connStr);
        conn.Open();
        using var c = conn.CreateCommand();

        var sb = new System.Text.StringBuilder("SELECT id, ts, level, category, metric, value, message FROM events");
        var conds = new List<string>();
        if (level.HasValue) { conds.Add("level=$lv"); c.Parameters.AddWithValue("$lv", level.Value.ToString().ToLowerInvariant()); }
        if (from.HasValue)  { conds.Add("ts>=$f");    c.Parameters.AddWithValue("$f",  from.Value); }
        if (to.HasValue)    { conds.Add("ts<$t");     c.Parameters.AddWithValue("$t",  to.Value); }
        if (conds.Count > 0) sb.Append(" WHERE ").AppendJoin(" AND ", conds);
        sb.Append(" ORDER BY ts DESC LIMIT $l");
        c.Parameters.AddWithValue("$l", limit);
        c.CommandText = sb.ToString();

        using var r = c.ExecuteReader();
        while (r.Read())
        {
            list.Add(new EventEntry(
                r.GetInt64(0),
                r.GetInt64(1),
                Enum.Parse<EventLevel>(r.GetString(2), ignoreCase: true),
                Enum.Parse<EventCategory>(r.GetString(3), ignoreCase: true),
                r.IsDBNull(4) ? null : r.GetString(4),
                r.IsDBNull(5) ? null : r.GetDouble(5),
                r.GetString(6)
            ));
        }
        return list;
    }
}