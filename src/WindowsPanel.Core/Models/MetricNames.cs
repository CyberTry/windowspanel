namespace WindowsPanel.Core.Models;

/// <summary>
/// 入库与 API 使用的指标名常量。前端订阅 WS 时也按这些键取值。
/// </summary>
public static class MetricNames
{
    public const string CpuTotal      = "cpu_total";
    public const string MemUsedBytes  = "mem_used";
    public const string GpuUtil       = "gpu_util";
    public const string VramUsedBytes = "vram_used";
    public const string NetTxBps      = "net_tx";
    public const string NetRxBps      = "net_rx";

    public static readonly string[] Persisted = { CpuTotal, MemUsedBytes, GpuUtil, VramUsedBytes, NetTxBps, NetRxBps };
}