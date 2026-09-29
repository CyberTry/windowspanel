namespace WindowsPanel.Server;

public sealed class PanelOptions
{
    public string? Url { get; set; }
    public AuthOptions?     Auth      { get; set; }
    public SamplingOptions? Sampling  { get; set; }
    public RetentionOptions? Retention { get; set; }
    public AlertsOptions?   Alerts    { get; set; }
}

public sealed class AuthOptions
{
    public bool    Enabled { get; set; }
    public string? Token   { get; set; }
}

public sealed class SamplingOptions
{
    public int LiveSeconds     { get; set; } = 1;
    public int PersistSeconds  { get; set; } = 5;
}

public sealed class RetentionOptions
{
    public int RawHours  { get; set; } = 24;
    public int Days      { get; set; } = 31;
    public int YearDays  { get; set; } = 365;
}

public sealed class AlertsOptions
{
    public double CpuPercent       { get; set; } = 90;
    public int    CpuMinutes       { get; set; } = 5;
    public double MemAvailPercent  { get; set; } = 10;
}