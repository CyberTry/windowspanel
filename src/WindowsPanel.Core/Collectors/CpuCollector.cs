using System.Diagnostics;
using System.Runtime.Versioning;
using WindowsPanel.Core.Models;

namespace WindowsPanel.Core.Collectors;

/// <summary>
/// 通过 PDH \Processor Information(_Total)\% Processor Utility 读取 CPU 使用率。
/// 多核机器合计可超过 100%（任务管理器口径，README §4.1）。
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class CpuCollector : ICollector
{
    private PerformanceCounter? counter;

    public string Name => MetricNames.CpuTotal;
    public int    ConsecutiveFailures { get; private set; }
    public double CpuPercent { get; private set; }

    public Task SampleAsync(CancellationToken ct)
    {
        try
        {
            counter ??= new PerformanceCounter("Processor Information", "% Processor Utility", "_Total", true);
            // 首次 NextValue 返回 0，需预热；调度器以 1s 间隔调用保证第二次起有效
            var v = counter.NextValue();
            if (!double.IsNaN(v) && v >= 0) CpuPercent = Math.Round(v, 1);
            ConsecutiveFailures = 0;
        }
        catch
        {
            ConsecutiveFailures++;
        }
        return Task.CompletedTask;
    }

    public void Dispose() => counter?.Dispose();
}