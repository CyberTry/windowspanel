using System.Net.NetworkInformation;
using System.Runtime.Versioning;
using WindowsPanel.Core.Models;

namespace WindowsPanel.Core.Collectors;

/// <summary>
/// 通过 NetworkInterface 差分计算网卡上下行速率（bytes/sec）。
/// 过滤 loopback / tunnel / 含 "Virtual" 的虚拟网卡；处理计数器回绕（负值视为 0）。
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class NetworkCollector : ICollector
{
    private readonly Dictionary<string, (long Tx, long Rx, DateTime T)> last = new();

    public string Name => MetricNames.NetRxBps;
    public int    ConsecutiveFailures { get; private set; }
    public long   TxBps { get; private set; }
    public long   RxBps { get; private set; }

    public Task SampleAsync(CancellationToken ct)
    {
        try
        {
            long tx = 0, rx = 0;
            var now = DateTime.UtcNow;
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                if (nic.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase)) continue;
                if (nic.Name.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase)) continue;

                var stats = nic.GetIPStatistics();
                var key = nic.Id;
                if (last.TryGetValue(key, out var prev))
                {
                    var dt = (now - prev.T).TotalSeconds;
                    if (dt > 0.5)
                    {
                        long dTx = stats.BytesSent - prev.Tx;
                        long dRx = stats.BytesReceived - prev.Rx;
                        if (dTx > 0) tx += (long)(dTx / dt);
                        if (dRx > 0) rx += (long)(dRx / dt);
                    }
                }
                last[key] = (stats.BytesSent, stats.BytesReceived, now);
            }
            TxBps = tx;
            RxBps = rx;
            ConsecutiveFailures = 0;
        }
        catch
        {
            ConsecutiveFailures++;
        }
        return Task.CompletedTask;
    }

    public void Dispose() { }
}