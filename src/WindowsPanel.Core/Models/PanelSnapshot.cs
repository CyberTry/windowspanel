namespace WindowsPanel.Core.Models;

/// <summary>
/// CollectorWorker 每秒组装一次的完整快照：实时指标 + 进程表 + 硬件。
/// 作为仪表盘 "GET /api/v1/summary" 的直接数据源。
/// </summary>
public sealed record PanelSnapshot(
    LiveSample                  Live,
    IReadOnlyList<ProcessInfo>  Processes,
    HardwareInfo                Hardware,
    DateTime                    StartedAt
);