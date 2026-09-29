namespace WindowsPanel.Core.Models;

public sealed record HardwareInfo(
    string   Os,
    string   Cpu,
    int      Cores,
    long     MemTotalBytes,
    string   Gpu,
    long     VramTotalBytes,
    string   Disk,
    string   HostName,
    TimeSpan Uptime
);