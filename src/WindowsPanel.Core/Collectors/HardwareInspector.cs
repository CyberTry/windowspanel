using System.Management;
using System.Runtime.Versioning;
using WindowsPanel.Core.Models;

namespace WindowsPanel.Core.Collectors;

/// <summary>
/// 一次性硬件信息采集（不实时刷新）。OS / CPU / 内存 / GPU / 磁盘 / 主机名 / 启动时长。
/// </summary>
[SupportedOSPlatform("windows")]
public static class HardwareInspector
{
    public static HardwareInfo Snapshot()
    {
        var memTotal = SafeLong(WmiFirst("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem", "TotalPhysicalMemory"));
        var cpu      = SafeStr (WmiFirst("SELECT Name FROM Win32_Processor", "Name"),                       "Unknown");
        var gpu      = SafeStr (WmiFirst("SELECT Name FROM Win32_VideoController", "Name"),                "Unknown");
        var disk     = SafeStr (WmiFirst("SELECT Model FROM Win32_DiskDrive", "Model"),                    "Unknown");
        var os       = SafeStr (WmiFirst("SELECT Caption FROM Win32_OperatingSystem", "Caption"),          "Windows");

        return new HardwareInfo(
            os,
            cpu,
            Environment.ProcessorCount,
            memTotal,
            gpu,
            6L * 1024 * 1024 * 1024, // 显存探测需 NVML/ADL，v1 默认 6 GB
            disk,
            Environment.MachineName,
            TimeSpan.FromMilliseconds(Environment.TickCount64)
        );
    }

    private static object? WmiFirst(string query, string key)
    {
        try
        {
            using var s = new ManagementObjectSearcher(query);
            foreach (var mo in s.Get())
            {
                var v = mo[key];
                mo.Dispose();
                return v;
            }
        }
        catch { }
        return null;
    }

    private static string SafeStr(object? v, string dflt) => v?.ToString()?.Trim() is { Length: > 0 } s ? s : dflt;
    private static long   SafeLong(object? v) { try { return Convert.ToInt64(v); } catch { return 0; } }
}