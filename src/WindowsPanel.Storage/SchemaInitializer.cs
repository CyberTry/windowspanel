using Microsoft.Data.Sqlite;

namespace WindowsPanel.Storage;

/// <summary>
/// SQLite 模式初始化（WAL + 三级 metric 表 + events + proc_snapshot）。
/// 详见 README §5.2。
/// </summary>
public sealed class SchemaInitializer
{
    private readonly string connStr;
    public SchemaInitializer(string dbPath) => connStr = $"Data Source={dbPath};";

    public void EnsureCreated()
    {
        using var conn = new SqliteConnection(connStr);
        conn.Open();
        Exec(conn, "PRAGMA journal_mode=WAL;");
        Exec(conn, "PRAGMA synchronous=NORMAL;");
        Exec(conn, "PRAGMA temp_store=MEMORY;");
        Exec(conn, SchemaSql);
    }

    private static void Exec(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS metric_raw (
          ts      INTEGER NOT NULL,
          metric  TEXT    NOT NULL,
          value   REAL    NOT NULL,
          PRIMARY KEY (metric, ts)
        ) WITHOUT ROWID;

        CREATE TABLE IF NOT EXISTS metric_1m (
          metric TEXT NOT NULL,
          ts     INTEGER NOT NULL,
          avg_v  REAL, max_v REAL, min_v REAL,
          PRIMARY KEY (metric, ts)
        ) WITHOUT ROWID;

        CREATE TABLE IF NOT EXISTS metric_1h (
          metric TEXT NOT NULL,
          ts     INTEGER NOT NULL,
          avg_v  REAL, max_v REAL, min_v REAL,
          PRIMARY KEY (metric, ts)
        ) WITHOUT ROWID;

        CREATE TABLE IF NOT EXISTS events (
          id INTEGER PRIMARY KEY AUTOINCREMENT,
          ts INTEGER NOT NULL,
          level TEXT NOT NULL,
          category TEXT NOT NULL,
          metric TEXT,
          value REAL,
          message TEXT
        );
        CREATE INDEX IF NOT EXISTS idx_events_ts ON events(ts);

        CREATE TABLE IF NOT EXISTS proc_snapshot (
          ts INTEGER, pid INTEGER, name TEXT,
          cpu REAL, mem BIGINT, net_tx BIGINT, net_rx BIGINT, gpu REAL
        );
        CREATE INDEX IF NOT EXISTS idx_proc_ts ON proc_snapshot(ts);
        """;
}