namespace WindowsPanel.Core.Models;

/// <summary>单个进程采集条目（任务管理器一行）。</summary>
public sealed record ProcessInfo(
    int    Pid,
    string Name,
    double Cpu,
    long   MemBytes,
    long   DiskReadBps,
    long   DiskWriteBps,
    long   NetTxBps,
    long   NetRxBps,
    double Gpu,
    string Status
);