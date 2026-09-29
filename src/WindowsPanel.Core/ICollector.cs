namespace WindowsPanel.Core;

/// <summary>所有数据采集器的统一接口。</summary>
public interface ICollector : IDisposable
{
    /// <summary>采集器名（用于日志 / 看门狗）。</summary>
    string Name { get; }

    /// <summary>连续失败次数；连续 ≥ 3 次即标记为 Degraded（不抛出）。</summary>
    int ConsecutiveFailures { get; }

    bool IsHealthy => ConsecutiveFailures < 3;

    /// <summary>执行一次采样。失败时内部计数 +1 但不抛出。</summary>
    Task SampleAsync(CancellationToken ct);
}