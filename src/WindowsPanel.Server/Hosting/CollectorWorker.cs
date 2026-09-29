using Microsoft.Extensions.Hosting;
using WindowsPanel.Core;
using WindowsPanel.Core.Aggregation;
using WindowsPanel.Core.Collectors;
using WindowsPanel.Core.Models;
using WindowsPanel.Server.Realtime;
using WindowsPanel.Storage;

namespace WindowsPanel.Server.Hosting;

/// <summary>
/// 每秒调度所有 Collector → 组装 PanelSnapshot → 推 WebSocket + 5s 持久化。
/// 这是整个服务的"心跳"——前端订阅 /ws 后看到的 1Hz 数据流全部来自此处。
/// </summary>
public sealed class CollectorWorker : BackgroundService
{
    private readonly IEnumerable<ICollector> collectors;
    private readonly PanelState state;
    private readonly WebSocketHub hub;
    private readonly RingBuffer ring;
    private readonly MetricStore store;
    private readonly HardwareInfo hardware;

    public CollectorWorker(
        IEnumerable<ICollector> collectors,
        PanelState state,
        WebSocketHub hub,
        RingBuffer ring,
        MetricStore store)
    {
        this.collectors = collectors;
        this.state      = state;
        this.hub        = hub;
        this.ring       = ring;
        this.store      = store;
        this.hardware   = HardwareInspector.Snapshot();
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var startedAt = DateTime.UtcNow;
        int tickCount = 0;

        while (!ct.IsCancellationRequested)
        {
            var t0 = DateTime.UtcNow;
            try
            {
                // 并行采集
                var tasks = collectors.Select(c => c.SampleAsync(ct)).ToArray();
                await Task.WhenAll(tasks);

                // 取出每个 collector 的最新值
                var cpu  = collectors.OfType<CpuCollector>().FirstOrDefault();
                var mem  = collectors.OfType<MemoryCollector>().FirstOrDefault();
                var gpu  = collectors.OfType<GpuCollector>().FirstOrDefault();
                var net  = collectors.OfType<NetworkCollector>().FirstOrDefault();
                var proc = collectors.OfType<ProcessCollector>().FirstOrDefault();

                var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var live = new LiveSample(
                    nowMs,
                    cpu?.CpuPercent ?? 0,
                    mem?.UsedPercent ?? 0, mem?.UsedBytes ?? 0, mem?.TotalBytes ?? 0,
                    gpu?.GpuPercent       ?? 0, gpu?.VramUsedPercent ?? 0, gpu?.VramUsedBytes ?? 0, gpu?.VramTotalBytes ?? 0,
                    net?.TxBps ?? 0, net?.RxBps ?? 0
                );
                ring.Push(live);

                var snap = new PanelSnapshot(live, proc?.Processes ?? Array.Empty<ProcessInfo>(), hardware, startedAt);
                state.Update(snap);

                // 广播实时
                _ = hub.BroadcastAsync("live", live);

                // 持久环（5s 一次）
                if (tickCount % 5 == 0)
                {
                    await store.InsertRawBatchAsync(new Dictionary<string, double>
                    {
                        [MetricNames.CpuTotal]      = live.Cpu,
                        [MetricNames.MemUsedBytes]  = live.MemUsedBytes,
                        [MetricNames.GpuUtil]       = live.Gpu,
                        [MetricNames.VramUsedBytes] = live.VramUsedBytes,
                        [MetricNames.NetTxBps]      = live.NetTxBps,
                        [MetricNames.NetRxBps]      = live.NetRxBps
                    }, nowMs);
                }
                tickCount++;
            }
            catch { /* 单帧失败不致命 */ }

            // 严格按 1s 节拍
            var elapsed = (DateTime.UtcNow - t0).TotalMilliseconds;
            var delay = Math.Max(50, 1000 - (int)elapsed);
            await Task.Delay(delay, ct);
        }
    }
}