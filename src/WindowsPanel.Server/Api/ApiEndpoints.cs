using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using WindowsPanel.Core;
using WindowsPanel.Core.Models;
using WindowsPanel.Server.Hosting;
using WindowsPanel.Storage;

namespace WindowsPanel.Server.Api;

public static class ApiEndpoints
{
    public static void Map(WebApplication app)
    {
        var api = app.MapGroup("/api/v1");

        api.MapGet("/summary", (PanelState st) =>
        {
            var snap = st.Get();
            if (snap is null) return Results.NotFound(new { error = "panel not ready" });

            var up = DateTime.UtcNow - snap.StartedAt;
            return Results.Ok(new SummaryDto(
                snap.Live,
                snap.Processes,
                snap.Hardware,
                up.ToString(@"hh\:mm\:ss"),
                snap.StartedAt
            ));
        });

        api.MapGet("/processes", (string? sort, string? order, string? q, PanelState st) =>
        {
            var procs = st.Get()?.Processes ?? Array.Empty<ProcessInfo>();

            IEnumerable<ProcessInfo> qry = procs;
            if (!string.IsNullOrEmpty(q))
                qry = qry.Where(p => p.Name.Contains(q, StringComparison.OrdinalIgnoreCase));

            qry = (sort?.ToLowerInvariant(), order?.ToLowerInvariant()) switch
            {
                ("cpu",  "asc")  => qry.OrderBy(p => p.Cpu),
                ("mem",  "asc")  => qry.OrderBy(p => p.MemBytes),
                ("mem",  "desc") => qry.OrderByDescending(p => p.MemBytes),
                ("pid",  "asc")  => qry.OrderBy(p => p.Pid),
                ("pid",  "desc") => qry.OrderByDescending(p => p.Pid),
                _                => qry.OrderByDescending(p => p.Cpu)
            };

            return Results.Ok(qry.Select(p => new ProcessDto(
                p.Pid,
                p.Name,
                p.Cpu,
                p.MemBytes,
                FormatBytes(p.MemBytes),
                p.DiskReadBps,
                p.DiskWriteBps,
                p.NetTxBps,
                p.NetRxBps,
                p.Gpu,
                p.Status
            )).ToList());
        });

        api.MapGet("/history", (string? metrics, string? range, long? from, long? to, MetricStore store) =>
        {
            var metricList = (metrics ?? "cpu_total,mem_used,gpu_util,net_tx,net_rx")
                .Split(',', StringSplitOptions.RemoveEmptyEntries);

            range ??= "1d";
            (string step, long defFrom, long defTo) rng = range switch
            {
                "1m"  => ("1m",  DateTimeOffset.UtcNow.AddDays(-31).ToUnixTimeMilliseconds(),  DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),
                "1y"  => ("1h",  DateTimeOffset.UtcNow.AddDays(-365).ToUnixTimeMilliseconds(), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),
                _     => ("raw", DateTimeOffset.UtcNow.AddHours(-24).ToUnixTimeMilliseconds(),  DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            };
            var f = from ?? rng.defFrom;
            var t = to   ?? rng.defTo;

            var series = new List<HistorySeriesDto>();
            foreach (var m in metricList)
            {
                IReadOnlyList<HistoryPointDto> points;
                if (rng.step == "raw")
                {
                    var rows = store.QueryRaw(m, f, t, 5000);
                    points = rows.Select(p => new HistoryPointDto(p.Ts, Math.Round(p.Value, 2))).ToList();
                }
                else if (rng.step == "1m")
                {
                    var rows = store.Query1m(m, f, t, 5000);
                    points = rows.Select(p => new HistoryPointDto(p.Ts, Math.Round(p.Avg, 2))).ToList();
                }
                else
                {
                    var rows = store.Query1h(m, f, t, 5000);
                    points = rows.Select(p => new HistoryPointDto(p.Ts, Math.Round(p.Avg, 2))).ToList();
                }
                series.Add(new HistorySeriesDto(m, points));
            }
            return Results.Ok(new HistoryDto(rng.step, series, Array.Empty<EventMarkDto>()));
        });

        api.MapGet("/events", (string? level, int? limit, EventStore store) =>
        {
            EventLevel? lv = level?.ToLowerInvariant() switch
            {
                "info"  => EventLevel.Info,
                "warn"  => EventLevel.Warn,
                "error" => EventLevel.Error,
                _       => null
            };
            var rows = store.Query(lv, limit ?? 200);
            return Results.Ok(rows.Select(e => new EventDto(
                e.Id,
                e.Ts,
                e.Level.ToString().ToLowerInvariant(),
                e.Category.ToString().ToLowerInvariant(),
                e.Metric,
                e.Value,
                e.Message
            )));
        });

        api.MapGet("/settings", (IOptions<PanelOptions> opt) =>
        {
            var p = opt.Value;
            return Results.Ok(new SettingsDto(
                p.Url ?? "http://127.0.0.1:9720",
                p.Auth?.Enabled ?? false,
                p.Sampling?.LiveSeconds ?? 1,
                p.Sampling?.PersistSeconds ?? 5,
                p.Alerts?.CpuPercent ?? 90,
                p.Alerts?.CpuMinutes ?? 5,
                p.Alerts?.MemAvailPercent ?? 10
            ));
        });
    }

    private static string FormatBytes(long b) => b switch
    {
        >= 1L << 30 => $"{b / (double)(1 << 30):F1} GB",
        >= 1L << 20 => $"{b / (double)(1 << 20):F1} MB",
        >= 1L << 10 => $"{b / (double)(1 << 10):F1} KB",
        _            => $"{b} B"
    };
}