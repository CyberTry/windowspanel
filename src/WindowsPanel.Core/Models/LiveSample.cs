namespace WindowsPanel.Core.Models;

/// <summary>1s 一帧的实时快照，直接通过 WebSocket 推送给前端。</summary>
public sealed record LiveSample(
    long   Ts,
    double Cpu,
    double MemUsedPct,  long MemUsedBytes,  long MemTotalBytes,
    double Gpu,
    double VramUsedPct, long VramUsedBytes, long VramTotalBytes,
    long   NetTxBps,
    long   NetRxBps
);