using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using WindowsPanel.Core;
using WindowsPanel.Core.Aggregation;
using WindowsPanel.Core.Collectors;
using WindowsPanel.Server;
using WindowsPanel.Server.Api;
using WindowsPanel.Server.Hosting;
using WindowsPanel.Server.Realtime;
using WindowsPanel.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<PanelOptions>(builder.Configuration.GetSection("Panel"));
var panelOpts = builder.Configuration.GetSection("Panel").Get<PanelOptions>() ?? new PanelOptions();
if (!string.IsNullOrWhiteSpace(panelOpts.Url))
    builder.WebHost.UseUrls(panelOpts.Url);

var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
Directory.CreateDirectory(dataDir);
var dbPath = Path.Combine(dataDir, "panel.db");

// Storage
builder.Services.AddSingleton(new SchemaInitializer(dbPath));
builder.Services.AddSingleton(new MetricStore(dbPath));
builder.Services.AddSingleton(new EventStore(dbPath));

// 实时窗口（最近 300 帧 ≈ 5 分钟）
builder.Services.AddSingleton(new RingBuffer(300));

// Collectors
builder.Services.AddSingleton<CpuCollector>();
builder.Services.AddSingleton<MemoryCollector>();
builder.Services.AddSingleton<GpuCollector>();
builder.Services.AddSingleton<NetworkCollector>();
builder.Services.AddSingleton<ProcessCollector>();
builder.Services.AddSingleton<ICollector>(sp => sp.GetRequiredService<CpuCollector>());
builder.Services.AddSingleton<ICollector>(sp => sp.GetRequiredService<MemoryCollector>());
builder.Services.AddSingleton<ICollector>(sp => sp.GetRequiredService<GpuCollector>());
builder.Services.AddSingleton<ICollector>(sp => sp.GetRequiredService<NetworkCollector>());
builder.Services.AddSingleton<ICollector>(sp => sp.GetRequiredService<ProcessCollector>());

// Realtime + State + Workers
builder.Services.AddSingleton<PanelState>();
builder.Services.AddSingleton<WebSocketHub>();
builder.Services.AddSingleton<AlertEvaluator>();
builder.Services.AddHostedService<CollectorWorker>();
builder.Services.AddHostedService(sp => new RetentionService(
    dbPath,
    TimeSpan.FromHours(panelOpts.Retention?.RawHours ?? 24),
    TimeSpan.FromDays(panelOpts.Retention?.Days ?? 31),
    TimeSpan.FromDays(panelOpts.Retention?.YearDays ?? 365)
));

if (WindowsServiceHelpers.IsWindowsService())
    builder.Services.AddWindowsService();

var app = builder.Build();
app.Services.GetRequiredService<SchemaInitializer>().EnsureCreated();

app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });
app.Services.GetRequiredService<WebSocketHub>().Attach(app);
ApiEndpoints.Map(app);

// 静态文件（前端构建产物由 src/WindowsPanel.Web 写入 wwwroot/）
app.UseDefaultFiles();
app.UseStaticFiles();

static void OpenBrowser(string url)
{
    try
    {
        using var _ = Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }
    catch { /* 忽略打开浏览器失败 */ }
}

// 本机浏览器打开用的地址（0.0.0.0 不可浏览，需换成 127.0.0.1）
static string LocalUrl(string url) => url.Replace("0.0.0.0", "127.0.0.1");

// 局域网内其他电脑访问用的地址（取首个内网 IPv4）
static string? LanUrl(int port)
{
    try
    {
        var lan = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up
                     && n.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Select(a => a.Address)
            .FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                               && !a.ToString().StartsWith("169.254."));
        return lan is null ? null : $"http://{lan}:{port}";
    }
    catch { return null; }
}

bool desktopMode = !WindowsServiceHelpers.IsWindowsService()
    && Environment.UserInteractive
    && OperatingSystem.IsWindows();

if (desktopMode)
{
    // 桌面模式：无控制台窗口，服务器后台运行，托盘图标常驻
    var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
    var browseUrl = LocalUrl(panelOpts.Url);
    var port = new Uri(panelOpts.Url).Port;
    var lanUrl = LanUrl(port);
    app.Lifetime.ApplicationStarted.Register(() => OpenBrowser(browseUrl));

    var hostTask = Task.Run(() => app.Run());

    using var iconStream = typeof(Program).Assembly
        .GetManifestResourceStream("WindowsPanel.Server.Assets.app.ico");
    using var icon = iconStream is not null ? new Icon(iconStream) : SystemIcons.Application;

    using var menu = new ContextMenuStrip();
    var openItem = new ToolStripMenuItem($"打开面板 ({browseUrl})");
    var lanItem = lanUrl is not null
        ? new ToolStripMenuItem($"局域网访问: {lanUrl}") { Enabled = false }
        : null;
    var exitItem = new ToolStripMenuItem("退出");
    openItem.Click += (_, _) => OpenBrowser(browseUrl);
    exitItem.Click += (_, _) =>
    {
        lifetime.StopApplication();   // 优雅停止服务器（数据落盘）
        Application.ExitThread();     // 结束托盘消息循环
    };
    menu.Items.AddRange(lanItem is not null
        ? new ToolStripItem[] { openItem, lanItem, new ToolStripSeparator(), exitItem }
        : new ToolStripItem[] { openItem, new ToolStripSeparator(), exitItem });

    using var tray = new NotifyIcon
    {
        Icon = icon,
        Text = "WindowsPanel — 运行中",
        ContextMenuStrip = menu,
        Visible = true
    };
    tray.DoubleClick += (_, _) => OpenBrowser(browseUrl);

    Application.SetHighDpiMode(HighDpiMode.SystemAware);
    Application.Run(); // 托盘消息循环（阻塞至点击"退出"）

    try { hostTask.GetAwaiter().GetResult(); } catch { /* 已请求停止 */ }
}
else
{
    // Windows 服务 / 非交互模式：保持原有控制台行为
    app.Run();
}