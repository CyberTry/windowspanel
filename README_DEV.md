# WindowsPanel — 开发说明

## 工程结构
```
WindowsPanel/
├── src/
│   ├── WindowsPanel.Core/      领域模型、Collector 接口、聚合
│   ├── WindowsPanel.Storage/   SQLite 仓储、归档清理
│   ├── WindowsPanel.Server/    ASP.NET Core Minimal API + WS + 托管服务
│   └── WindowsPanel.Web/       Vue 3 + Vite + ECharts 前端
├── WindowsPanel.sln
└── README_DEV.md
```

## 前置环境
- .NET 8 SDK（https://dotnet.microsoft.com/download/dotnet/8.0 ）
- Node.js 20+ 与 pnpm/npm

## 构建与运行

### 后端
```powershell
dotnet build WindowsPanel.sln
dotnet run --project src/WindowsPanel.Server
```
启动后访问 http://127.0.0.1:9720 （前端构建后会嵌入 wwwroot）

### 前端（开发模式）
```powershell
cd src/WindowsPanel.Web
pnpm install
pnpm dev
```

### 前端（构建并嵌入 Server）
```powershell
cd src/WindowsPanel.Web
pnpm build      # 产物自动写入 ../WindowsPanel.Server/wwwroot/
```

## 发布
```powershell
dotnet publish src/WindowsPanel.Server -c Release -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:PublishTrimmed=true
```
产物在 `src/WindowsPanel.Server/bin/Release/net8.0-windows/win-x64/publish/WindowsPanel.exe`，
内嵌前端单文件可执行，约 30-50 MB。

## API 路径（基础前缀 `/api/v1`）
- `GET /summary`     仪表盘六指标 + 硬件 + 运行时长
- `GET /processes`   进程列表（?sort=cpu&order=desc&q=...）
- `GET /history`     历史曲线（?metrics=...&range=1d|1m|1y）
- `GET /events`      事件日志（?level=warn&limit=200）
- `GET /settings`    配置读取
- `WS  /ws`          实时推送（每 1s）