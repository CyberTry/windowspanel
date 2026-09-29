using WindowsPanel.Core.Models;

namespace WindowsPanel.Core.Collectors;

/// <summary>
/// GPU 采集器 v1：探测 PDH `\GPU Engine(*)\Utilization Percentage` 与
/// `\GPU Adapter Memory(*)\Dedicated Usage`。在管理员 + 驱动齐全时可工作；否则
/// 连续失败 3 次后进入 Degraded 模式，返回零值（前端卡片显示"不可用"）。
///
/// 生产部署若需更高精度/兼容性，可替换为 NVML / AMD ADL，本类对外属性不变。
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class GpuCollector : ICollector
{
    private System.Diagnostics.PerformanceCounter[]? utilCounters;
    private System.Diagnostics.PerformanceCounter[]? memCounters;

    public string Name => MetricNames.GpuUtil;
    public int    ConsecutiveFailures { get; private set; }
    public bool   Degraded { get; private set; }

    public double GpuPercent       { get; private set; }
    public double VramUsedPercent  { get; private set; }
    public long   VramUsedBytes    { get; private set; }
    public long   VramTotalBytes   { get; private set; }

    public Task SampleAsync(CancellationToken ct)
    {
        if (Degraded) return Task.CompletedTask;
        try
        {
            utilCounters ??= DiscoverInstances("GPU Engine", "Utilization Percentage");
            memCounters  ??= DiscoverInstances("GPU Adapter Memory", "Dedicated Usage");

            double utilSum = 0;
            if (utilCounters.Length > 0)
            {
                foreach (var c in utilCounters) utilSum += c.NextValue();
                GpuPercent = Math.Round(utilSum / utilCounters.Length, 1);
            }
            else
            {
                GpuPercent = 0;
            }

            long memSum = 0;
            if (memCounters.Length > 0)
            {
                foreach (var c in memCounters) memSum += (long)c.NextValue();
                VramUsedBytes  = memSum;
                VramTotalBytes = VramTotalBytes == 0 ? 6L * 1024 * 1024 * 1024 : VramTotalBytes;
                VramUsedPercent = Math.Round((double)VramUsedBytes / VramTotalBytes * 100, 1);
            }
            ConsecutiveFailures = 0;
        }
        catch
        {
            ConsecutiveFailures++;
            if (ConsecutiveFailures >= 3) Degraded = true;
        }
        return Task.CompletedTask;
    }

    private static System.Diagnostics.PerformanceCounter[] DiscoverInstances(string category, string counter)
    {
        try
        {
            var c = new System.Diagnostics.PerformanceCounterCategory(category);
            var instances = c.GetInstanceNames();
            var arr = new System.Diagnostics.PerformanceCounter[instances.Length];
            for (int i = 0; i < instances.Length; i++)
                arr[i] = new System.Diagnostics.PerformanceCounter(category, counter, instances[i], true);
            return arr;
        }
        catch
        {
            return Array.Empty<System.Diagnostics.PerformanceCounter>();
        }
    }

    public void Dispose()
    {
        if (utilCounters != null) foreach (var c in utilCounters) c.Dispose();
        if (memCounters  != null) foreach (var c in memCounters)  c.Dispose();
    }
}