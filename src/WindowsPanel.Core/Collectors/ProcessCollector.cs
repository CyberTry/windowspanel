using System.Diagnostics;
using System.Runtime.Versioning;
using WindowsPanel.Core.Models;

namespace WindowsPanel.Core.Collectors;

/// <summary>
/// 进程采集器：枚举所有进程，CPU 通过两次 TotalProcessorTime 差分（任务管理器口径）。
/// 进程 CPU 不入库，仅实时推送给仪表盘；进程内存可入库用于回溯（proc_snapshot）。
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ProcessCollector : ICollector
{
    public string Name => "processes";
    public int    ConsecutiveFailures { get; private set; }
    public IReadOnlyList<ProcessInfo> Processes { get; private set; } = Array.Empty<ProcessInfo>();

    private readonly Dictionary<int, (DateTime T, TimeSpan Cpu)> prev = new();

    public Task SampleAsync(CancellationToken ct)
    {
        try
        {
            var now = DateTime.UtcNow;
            var list = new List<ProcessInfo>();
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    var pid = p.Id;
                    var cpu = Safe(() => p.TotalProcessorTime, TimeSpan.Zero);
                    var mem = Safe(() => p.WorkingSet64, 0L);
                    var name = Safe(() => p.ProcessName, "?");
                    double pct = 0;
                    if (prev.TryGetValue(pid, out var pv))
                    {
                        var dt = (now - pv.T).TotalSeconds;
                        if (dt > 0)
                        {
                            var dc = (cpu - pv.Cpu).TotalSeconds;
                            pct = dc / dt * 100.0;
                            if (pct < 0 || pct > 6400) pct = 0; // 异常值过滤
                        }
                    }
                    prev[pid] = (now, cpu);
                    list.Add(new ProcessInfo(pid, name, Math.Round(pct, 1), mem, 0, 0, 0, 0, 0, "运行"));
                }
                catch { /* 进程可能在采样时消失 */ }
                finally { p.Dispose(); }
            }

            // 清理已退出进程的缓存
            var alive = new HashSet<int>(list.Select(x => x.Pid));
            foreach (var k in prev.Keys.ToList())
                if (!alive.Contains(k)) prev.Remove(k);

            Processes = list;
            ConsecutiveFailures = 0;
        }
        catch
        {
            ConsecutiveFailures++;
        }
        return Task.CompletedTask;
    }

    private static T Safe<T>(Func<T> f, T dflt)
    {
        try { return f(); } catch { return dflt; }
    }

    public void Dispose() { }
}