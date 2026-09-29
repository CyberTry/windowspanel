using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using WindowsPanel.Core.Models;
using WindowsPanel.Storage;

namespace WindowsPanel.Server.Hosting;

/// <summary>
/// 简单的阈值评估（v1.0 仅写事件日志，不推送通知）。
///  - CPU 持续 > 阈值 N 分钟 → 写一条 Warn（连续触发只写一次，恢复后再触发）
///  - 可用内存 < 阈值 → 写一条 Warn（同样去抖）
/// v1.1 规划：Webhook / 邮件推送。
/// </summary>
public sealed class AlertEvaluator : BackgroundService
{
    private readonly PanelState state;
    private readonly EventStore events;
    private readonly PanelOptions opts;

    private DateTime _cpuHighSince = DateTime.MinValue;
    private bool _cpuAlerted;
    private bool _memAlerted;

    public AlertEvaluator(PanelState state, EventStore events, IOptions<PanelOptions> opts)
    {
        this.state   = state;
        this.events  = events;
        this.opts    = opts.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var snap = state.Get();
                if (snap is not null) Evaluate(snap);
            }
            catch { /* 单帧评估失败忽略 */ }
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }

    private void Evaluate(PanelSnapshot snap)
    {
        var now = DateTime.UtcNow;
        var cpuThreshold = opts.Alerts?.CpuPercent ?? 90;
        var memThreshold = opts.Alerts?.MemAvailPercent ?? 10;
        var cpuMinutes   = opts.Alerts?.CpuMinutes ?? 5;

        // CPU 持续高载
        if (snap.Live.Cpu > cpuThreshold)
        {
            if (_cpuHighSince == DateTime.MinValue) _cpuHighSince = now;
            if (!_cpuAlerted && (now - _cpuHighSince).TotalMinutes >= cpuMinutes)
            {
                events.Insert(
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    EventLevel.Warn, EventCategory.Threshold,
                    $"CPU {snap.Live.Cpu:F1}% 持续 {cpuMinutes} 分钟（阈值 {cpuThreshold}%）",
                    MetricNames.CpuTotal, snap.Live.Cpu
                );
                _cpuAlerted = true;
            }
        }
        else
        {
            _cpuHighSince = DateTime.MinValue;
            _cpuAlerted   = false;
        }

        // 可用内存压力
        var availPct = 100 - snap.Live.MemUsedPct;
        if (availPct < memThreshold && !_memAlerted)
        {
            events.Insert(
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                EventLevel.Warn, EventCategory.Threshold,
                $"可用内存 < {memThreshold}%（当前已用 {snap.Live.MemUsedPct:F1}%）",
                MetricNames.MemUsedBytes, snap.Live.MemUsedPct
            );
            _memAlerted = true;
        }
        else if (availPct >= memThreshold) _memAlerted = false;
    }
}