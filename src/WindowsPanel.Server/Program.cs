using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
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

app.Run();