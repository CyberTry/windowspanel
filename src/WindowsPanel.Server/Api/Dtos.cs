using WindowsPanel.Core.Models;

namespace WindowsPanel.Server.Api;

public sealed record SummaryDto(
    LiveSample                  Live,
    IReadOnlyList<ProcessInfo>  Processes,
    HardwareInfo                Hardware,
    string                      Uptime,
    DateTime                    StartedAt
);

public sealed record ProcessDto(
    int    Pid,
    string Name,
    double Cpu,
    long   MemBytes,
    string MemFmt,
    long   DiskRead,
    long   DiskWrite,
    long   NetTx,
    long   NetRx,
    double Gpu,
    string Status
);

public sealed record HistoryPointDto(long Ts, double Value);
public sealed record HistorySeriesDto(string Metric, IReadOnlyList<HistoryPointDto> Points);
public sealed record EventMarkDto(long Ts, string Label);
public sealed record HistoryDto(
    string Step,
    IReadOnlyList<HistorySeriesDto> Series,
    IReadOnlyList<EventMarkDto>     Events
);

public sealed record EventDto(
    long    Id,
    long    Ts,
    string  Level,
    string  Category,
    string? Metric,
    double? Value,
    string  Message
);

public sealed record SettingsDto(
    string Url,
    bool   AuthEnabled,
    int    LiveSeconds,
    int    PersistSeconds,
    double CpuPercent,
    int    CpuMinutes,
    double MemAvailPercent
);