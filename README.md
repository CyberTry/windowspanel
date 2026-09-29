# WindowsPanel — Windows 服务器 Web 管理面板设计文档

> 版本：v1.0（设计方案）
> 目标平台：Windows 10 / Windows 11 / Windows Server 2016+
> 交付形态：单机安装的本地服务 + 浏览器访问的 Web 管理界面

---

## 1. 项目概述

### 1.1 背景

在普通 Windows 10/11 机器上部署一个轻量级管理面板，运维人员无需登录远程桌面、无需安装大型监控系统，直接通过浏览器即可查看本机的实时状态与历史负载，并能像"任务管理器"一样查看每个应用的资源占用。

### 1.2 设计目标

| 编号 | 目标 | 说明 |
|------|------|------|
| G1 | 实时监控 | CPU 使用率、内存占用、GPU 使用率、显存占用、网络上下行速率，秒级刷新 |
| G2 | 进程级明细 | 任务管理器级别的每应用/进程 CPU、内存、磁盘、网络、GPU 占用列表 |
| G3 | 历史负载曲线 | 一天 / 一个月 / 一年三个时间粒度的历史曲线与事件日志 |
| G4 | 零依赖安装 | 单一可执行程序注册为 Windows 服务，安装即用，不需要 IIS / 数据库服务器 |
| G5 | 浏览器访问 | 现代浏览器（Chrome / Edge / Firefox）直接访问，前端零安装 |
| G6 | 低开销 | 常驻内存 < 100MB，CPU 平均占用 < 1.5%，不干扰业务 |

### 1.3 非目标（本期不做）

- 多主机集中纳管 / 集群视图（架构上预留扩展点）
- 自动告警推送（邮件/Webhook 在 v1.1 规划，v1.0 仅记录事件日志）
- 远程执行命令、关机重启等操作类功能（安全风险高，单列为 v2.0 规划）

---

## 2. 总体架构

```
┌─────────────────────────────────────────────────────────────┐
│                        浏览器（Web GUI）                      │
│   仪表盘 / 进程页 / 历史曲线 / 事件日志 / 设置                  │
└───────────────▲──────────────────────────────▲──────────────┘
                │ HTTP/REST (查询)              │ WebSocket (实时推送)
┌───────────────┴──────────────────────────────┴──────────────┐
│                  WindowsPanel.Server（单进程）                │
│  ┌────────────┐  ┌────────────┐  ┌────────────────────────┐ │
│  │ 采集调度器   │→│ 指标聚合层   │→│ 存储引擎（SQLite+归档）  │ │
│  │ (1s / 5s)  │  │ (RingBuffer)│  │  raw / 1m / 1h 粒度    │ │
│  └─────▲──────┘  └────────────┘  └────────────────────────┘ │
│        │                                                     │
│  ┌─────┴──────────────────────────────────────────────────┐ │
│  │ 数据采集器 (Collectors)                                  │ │
│  │  CPUCollector │ MemCollector │ GpuCollector │ NetCollector │
│  │  ProcessCollector (CPU/内存/磁盘/网络/GPU per-PID)        │ │
│  └─────▲──────────────────────────────────────────────────┘ │
└────────┼────────────────────────────────────────────────────┘
         │ PDH / Performance Counters / ETW / Win32 API
┌────────┴────────────────────────────────────────────────────┐
│              Windows 10 内核与驱动（被监控主机）                │
│   Processor  Counter │ GPU Engine Counter │ TCPIP ETW        │
└─────────────────────────────────────────────────────────────┘
```

### 2.1 组件说明

| 组件 | 职责 |
|------|------|
| **采集调度器** | 两个采样环：实时环（1s，供仪表盘）与持久环（5s，供历史曲线与日志） |
| **数据采集器** | 每个指标一个独立 Collector 接口实现，互不阻塞，单个采集器异常不影响整体 |
| **指标聚合层** | 内存环形缓冲（最近 5 分钟原始点），供 WebSocket 快速推送 |
| **存储引擎** | SQLite 单文件库，三级粒度（5s 原始 → 1 分钟 → 1 小时）自动降采样与过期清理 |
| **Web 服务** | 内嵌 HTTP 静态资源 + REST API + WebSocket，绑定 127.0.0.1，可配置放开 |
| **Windows 服务** | 以 LocalSystem 或 Performance Log Users 组身份常驻，开机自启 |

### 2.2 技术选型

| 层 | 选型 | 备选 | 选择理由 |
|----|------|------|----------|
| 后端语言 | **C# / .NET 8** | Go + pdh-go | 一等公民访问 PDH/ETW/Win32；`dotnet publish` 出单文件自包含 exe，目标机无需装运行时 |
| 实时数据 | **PDH 性能计数器** | WMI（慢）、ETW | PDH 是任务管理器同源数据源，开销低 |
| 进程网络 | **ETW (Kernel Network)** | 轮询连接表 | Windows 无 per-PID 网络计数器，ETW 是唯一准确途径 |
| 存储 | **SQLite**（WAL 模式） | InfluxDB、TimescaleDB | 单文件零部署，亿级点位足够一年 5s 采样 |
| 前端 | **Vue 3 + Vite + ECharts** | React | ECharts 对时间序列缩放（dataZoom）支持最好，中文文档全 |
| 实时通道 | **WebSocket** | SSE | 双向、可复用心跳，便于后续做指令类功能 |
| 服务化 | **.NET Worker Service + WindowsServiceLifetime** | NSSM、sc create | 官方方案，支持优雅退出与故障自动重启 |

---

## 3. 功能需求与页面设计

### 3.1 信息架构

```
WindowsPanel Web UI
├── 仪表盘（Dashboard）           —— 实时状态总览
├── 进程（Processes）             —— 任务管理器式明细
├── 历史曲线（History）            —— 1天 / 1月 / 1年 负载曲线
├── 事件日志（Logs）               —— 阈值事件、服务运行日志
└── 设置（Settings）               —— 采样、告警阈值、端口、导出
```

### 3.2 仪表盘

- **六宫格指标卡**：CPU%、内存（已用/总量+条形图）、GPU%、显存（已用/总量）、上行速率、下行速率；每卡含最近 5 分钟迷你趋势线。
- **主图表**：CPU / 内存 / GPU 三线叠加的 5 分钟实时曲线（1s 刷新，WebSocket 推送）。
- **网络图**：上下行双面积图 + 累计量（今日流量）。
- **顶部状态栏**：运行时长、主机名、CPU 型号、GPU 型号、当前时间、服务状态灯。

### 3.3 进程页（对标任务管理器）

表格列（默认按 CPU 降序，点击任意列排序）：

| 列 | 说明 |
|----|------|
| 应用/进程名 | 进程图标（从 exe 提取）+ 名称 + PID |
| CPU % | 双采样差分法计算（同任务管理器口径），多核可超过 100% 可切换"按总核归一" |
| 内存 (专用) | Working Set - private，显示 GB/MB 与占比 |
| 磁盘活动 | IO 读写速率（B/s） |
| 网络 | 该 PID 上/下行速率（ETW 采集） |
| GPU | 该 PID 所有 GPU Engine 引擎利用率之和（3D/Copy/Video Decode） |
| 显存 | 该 PID 的 GPU 专用内存占用 |
| 线程/句柄/启动时间 | 辅助列，可选显示 |

功能：搜索过滤、按名称分组（"应用"视图 vs "详细信息"视图切换）、右键菜单（结束任务 —— v2.0 开启，v1.0 仅"打开文件位置/复制信息"）、底部状态条（进程总数、句柄、线程数）。

### 3.4 历史曲线页

- 时间范围切换：**今天（5s 粒度）/ 本月（1 分钟粒度）/ 本年（1 小时粒度）**，快捷按钮 + 任意起止时间选择。
- 指标多选叠加：CPU、内存、GPU、显存、网络上行、网络下行、（扩展）磁盘 IO。
- 图例开关、dataZoom 拖拽缩放、十字准星读值、导出 CSV / PNG。
- 曲线上可标注事件点（如"CPU > 90% 持续 5 分钟"发生的时刻），点击跳转日志详情。

### 3.5 事件日志页

| 日志类型 | 内容 |
|----------|------|
| 性能事件 | 阈值触发记录：`[2025-06-01 14:23] CPU 95.2% 持续 5分12秒（阈值 90%）` |
| 服务事件 | 服务启动/停止、采集器异常、数据库轮转、登录/认证失败 |
| 操作审计 | 配置修改、（v2.0）远程操作记录 |

支持级别过滤（信息/警告/错误）、关键字搜索、时间范围筛选、导出。

---

## 4. 数据采集设计（核心）

### 4.1 指标 → 数据源映射

| 指标 | 数据源（PDH 计数器 / API） | 采样周期 | 备注 |
|------|---------------------------|----------|------|
| 总 CPU% | `\Processor Information(_Total)\% Processor Utility` | 1s | 比旧的 `% Processor Time` 更准确（含睿频），任务管理器同源 |
| 每核 CPU% | `\Processor Information(*)\% Processor Utility` | 1s | 仪表盘可展开每核 |
| 内存占用 | `GlobalMemoryStatusEx`（Available/Total） | 1s | 已用 = 总量 - 可用 - (可削减的系统缓存按需) |
| 内存细分 | `\Memory\Committed Bytes`、`Pool Paged/Nonpaged` | 5s | 用于日志与详情 |
| GPU 使用率 | `\GPU Engine(*)\Utilization Percentage` | 1s | 汇总所有引擎实例 → 全卡利用率；含 pid 实例名可归因进程 |
| 显存占用 | `\GPU Adapter Memory(*)\Dedicated Usage` + `Shared Usage` | 1s | 无独显时该计数器可能缺失，需优雅降级 |
| 网络上下行 | `\Network Interface(*)\Bytes Sent/sec`、`Bytes Received/sec` | 1s | 过滤 Loopback/Pseudo 接口后求和；处理计数器回绕 |
| 每进程 CPU | `Process.GetTotalProcessorTime()` 两次采样差分 | 2s | Task Manager 同口径，避免 PDH 进程实例名 `#1` 重复问题 |
| 每进程内存 | `WorkingSetPrivate` / `PrivateBytes`（PDH `\Process(*)\ID Process` 匹配） | 2s | 按 PID 而非实例名匹配，防止进程重名 |
| 每进程磁盘 | `\Process(*)\IO Data Bytes/sec` + `IO Other Bytes` | 2s | |
| 每进程网络 | **ETW** `Microsoft-Windows-Kernel-Network`（PidStart/File... 事件） | 实时流 | 仅 ETW 能按 PID 拆分收发字节 |
| 每进程 GPU | `\GPU Engine(*)\Utilization Percentage` 实例名 `{pid}.{engtype}.{luid}` | 2s | 解析实例名反查 PID，按引擎类型归类 |
| 进程树/图标/启动时间 | `NtQueryInformationProcess` / `Process` 类 / `SHGetFileInfo` | 变更时 | |

> **采集器接口**（每个指标独立实现，故障隔离）：
> ```csharp
> public interface ICollector<T> : IDisposable
> {
>     string Name { get; }
>     Task<T> SampleAsync(CancellationToken ct);
> }
> ```

### 4.2 采样与降级策略

- **双环采样**：1s 实时环只写内存（RingBuffer + WebSocket）；5s 持久环写库。降低写放大。
- **看门狗**：单个 Collector 连续失败 3 次 → 标记为 `Degraded`，前端显示"GPU 数据不可用"，每 60s 重试恢复；不拖垮其他指标。
- **权限**：以 `Performance Log Users` + `LocalSystem` 运行；GPU 计数器需要管理员，服务默认 LocalSystem。
- **多 GPU**：计数器实例含 ` videoidx`，按 Adapter 实例分别建模，前端可切换显卡。
- **计数器回绕/重置**：网络速率用"本次值 - 上次值 / Δt"并检测负值跳变（接口禁用重启用 0 填充）。

### 4.3 进程 CPU 计算公式（任务管理器口径）

```
cpu% = (Δ processTotalProcessorTime / Δ wallClock) × 100
```
多线程进程可 > 100%；提供"按逻辑核数归一化"开关（÷ processorCount）。

---

## 5. 存储与历史日志设计

### 5.1 三级粒度（Rollup）

| 粒度 | 采样/聚合间隔 | 保留时长 | 服务页面 | 单指标一年数据量（约） |
|------|--------------|----------|----------|----------------------|
| **raw** | 5 秒 | **24 小时** | "今天"曲线 | 17,280 点 × 8B ≈ 0.3MB |
| **1m** | 1 分钟（avg/max/min 聚合） | **31 天** | "本月"曲线 | 525,600 点 × 16B ≈ 8MB |
| **1h** | 1 小时（avg/max/min 聚合） | **365 天** | "本年"曲线 | 8,760 点 × 16B ≈ 0.2MB |

> 三项聚合值（avg/max/min）同时保留，保证"本月曲线"上仍能看到峰值毛刺。
> 全部 8 个指标 × 3 粒度，一年总库体积预估 **< 500MB**（含索引与事件日志）。

### 5.2 SQLite Schema（核心表）

```sql
-- 秒级原始样本（5s）
CREATE TABLE metric_raw (
  ts      INTEGER NOT NULL,   -- Unix ms
  metric  TEXT    NOT NULL,   -- cpu_total / mem_used / gpu_util / net_tx ...
  value   REAL    NOT NULL,
  PRIMARY KEY (metric, ts)
) WITHOUT ROWID;

-- 分钟聚合
CREATE TABLE metric_1m (
  metric TEXT NOT NULL, ts INTEGER NOT NULL,
  avg_v REAL, max_v REAL, min_v REAL,
  PRIMARY KEY (metric, ts)
) WITHOUT ROWID;

-- 小时聚合（结构同 1m）
CREATE TABLE metric_1h (...);

-- 事件日志
CREATE TABLE events (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  ts INTEGER NOT NULL,
  level TEXT NOT NULL,        -- info / warn / error
  category TEXT NOT NULL,     -- threshold / service / audit
  metric TEXT, value REAL, message TEXT
);
CREATE INDEX idx_events_ts ON events(ts);

-- 进程快照（可选，抽样存储 Top10 进程，用于回溯"当时是谁占的"）
CREATE TABLE proc_snapshot (
  ts INTEGER, pid INTEGER, name TEXT,
  cpu REAL, mem BIGINT, net_tx BIGINT, net_rx BIGINT, gpu REAL
);
```

### 5.3 归档流水线

```
5s 采样 ──写入──> metric_raw ──每分钟──> 聚合写 metric_1m ──每小时──> 聚合写 metric_1h
                      │                        │                        │
                过期(>24h)删除           过期(>31d)删除           过期(>365d)删除
```
- 聚合作业在内存中增量计算（max/min/sum/count），整点事务提交，避免查询时现算。
- WAL 模式 + 每日 `PRAGMA incremental_vacuum`，维护窗口记录到事件日志。
- **查询 API 自动选粒度**：请求跨度 ≤ 24h → raw；≤ 31d → 1m；否则 → 1h（可 `?step=` 强制）。

### 5.4 事件日志（告警规则 v1.0）

| 规则 | 默认阈值 | 记录内容 |
|------|----------|----------|
| CPU 持续高载 | > 90% 持续 5 分钟 | 起止时间、峰值、期间 Top5 进程 |
| 内存压力 | 可用内存 < 10% | 起止时间、提交率 |
| 显存压力 | 专用显存 > 95% | 峰值 |
| 网络异常 | 上/下行 > 阈值持续 1 分钟 | 用户可配 |
| 服务异常 | 采集器降级/恢复、服务重启 | 错误详情 |

---

## 6. API 设计（REST + WebSocket）

Base：`http://127.0.0.1:9720/api/v1`（端口可配）

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/summary` | 仪表盘六指标 + 硬件信息 + 运行时长 |
| GET | `/processes?sort=cpu&order=desc&q=chrome` | 进程列表快照 |
| GET | `/history?metrics=cpu_total,gpu_util&from=&to=&step=auto` | 历史曲线（自动选粒度），支持 `range=1d\|1m\|1y` 快捷 |
| GET | `/events?level=warn&from=&to=&limit=200` | 事件日志分页查询 |
| GET | `/export?...&format=csv` | CSV 导出 |
| GET/PUT | `/settings` | 配置读写 |
| POST | `/auth/login` | Token 登录（启用鉴权时） |
| WS | `/ws` | 实时推送（见下） |

**WebSocket 消息示例**

```jsonc
// server → client, 每 1s
{ "type": "live", "ts": 1735689600000,
  "cpu": 23.5, "memUsed": 10737418240, "memTotal": 34359738368,
  "gpu": 41.0, "vramUsed": 3221225472, "vramTotal": 8589934592,
  "netTx": 131072, "netRx": 2097152 }
```
客户端订阅粒度可选 `live` / `5s`；断线自动重连并补拉最近 5 分钟快照。

---

## 7. 部署与安装

### 7.1 安装方式

1. **安装包（推荐）**：Inno Setup 生成 `WindowsPanel-Setup.exe`
   - 拷贝 `WindowsPanel.exe` → `C:\Program Files\WindowsPanel\`
   - `sc create WindowsPanel binPath=... start=auto` 或使用 `New-Service` 注册服务
   - 写入防火墙规则（仅当用户选择"允许远程访问"时，端口默认 9720）
   - 写入卸载信息，卸载时停止并删除服务
2. **免安装绿色版**：`install.ps1`（管理员）注册服务；`uninstall.ps1` 反向清理。

### 7.2 发布

```powershell
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:PublishTrimmed=true
```
产物约 30–50MB 单 exe（内嵌前端静态资源），目标机 **无需安装 .NET 运行时**。

### 7.3 配置文件 `appsettings.json`

```jsonc
{
  "Panel": {
    "Url": "http://127.0.0.1:9720",   // 改成 0.0.0.0 开放远程
    "Auth": { "Enabled": true, "Token": "" },   // 空则首次启动随机生成并打印
    "Sampling": { "LiveSeconds": 1, "PersistSeconds": 5 },
    "Retention": { "RawHours": 24, "Days": 31, "YearDays": 365 },
    "Alerts": { "CpuPercent": 90, "CpuMinutes": 5, "MemAvailPercent": 10 }
  }
}
```

### 7.4 运行身份

| 方案 | 能力 | 建议 |
|------|------|------|
| LocalSystem（默认） | 全部计数器、可结束进程（v2.0） | ✅ 默认 |
| 专用低权限账户 + Performance Log Users | CPU/内存/网络可采，部分 GPU 计数器可能拒绝 | 高安全环境备选 |

---

## 8. 安全设计

- 默认**只绑定 127.0.0.1**；开放局域网需在设置中显式开启。
- 启用鉴权：登录后签发 JWT（2h 有效），WebSocket 连接携带 token；连续失败 5 次锁定 10 分钟并记入事件日志。
- 生产建议反向代理 + HTTPS（文档提供 Caddy/Nginx 示例），或内置自签证书开关。
- CSRF 防护（PUT/POST 校验 Origin）、CSP 头、静态资源防篡改（内容哈希）。
- 所有查询 API 只读；v1.0 不提供任何系统变更接口。
- 日志与数据库文件 ACL 限制为管理员组可读写。

---

## 9. 代码结构

```
WindowsPanel/
├── src/
│   ├── WindowsPanel.Core/          # 领域模型、Collector 接口、指标定义
│   │   ├── Collectors/             # Cpu / Mem / Gpu / Net / Process / ProcessNetEtw
│   │   ├── Aggregation/            # RingBuffer、Rollup 聚合器
│   │   └── Models/
│   ├── WindowsPanel.Storage/       # SQLite 仓储、迁移、Retention 清理
│   ├── WindowsPanel.Server/        # ASP.NET Core 最小 API + WebSocket + 服务宿主
│   │   ├── Api/                    # 端点、DTO、鉴权
│   │   ├── Hosting/                # WindowsServiceLifetime、看门狗
│   │   └── wwwroot/                # 前端构建产物（内嵌）
│   └── WindowsPanel.Web/           # Vue3 + Vite + ECharts 前端源码
├── installer/                      # Inno Setup 脚本、install.ps1
├── tests/                          # 单元测试 + 计数器集成测试
├── docs/                           # API 文档、部署手册
└── README.md                       # 本文件
```

---

## 10. 关键风险与对策

| # | 风险 | 影响 | 对策 |
|---|------|------|------|
| R1 | 部分机器 GPU 性能计数器缺失/被驱动禁用（尤其虚拟机、核显旧驱动） | 无 GPU 数据 | 启动时探测计数器可用性，自动降级为"NVIDIA NVML / AMD ADL"备选源，再不可用则卡片显示"不可用" |
| R2 | ETW 进程网络采集需要管理员权限 | 每进程网络列为空 | 默认 LocalSystem 运行；无权限时隐藏该列并提示 |
| R3 | PDH 进程实例名重复（`chrome#1`） | 进程数据错配 | 统一用差分法 + PID 匹配，不依赖实例名 |
| R4 | 高进程数（>800）时 1s 采样开销大 | CPU 抬升 | 进程采集降为 2s；采集器并行化；性能基准守住 <1.5% |
| R5 | SQLite 写放大影响寿命 | 磁盘 | WAL + 批量写（5s 一次事务）；支持数据库目录重定向到非系统盘 |
| R6 | 浏览器时间范围查询数据量大 | 接口慢 | 服务端按跨度自动选粒度 + 降采样上限（每曲线 ≤ 2000 点返回） |

---

## 11. 验收标准

1. 目标机安装后 1 分钟内可通过浏览器打开面板，服务开机自启。
2. 实时六指标刷新延迟 ≤ 2s，与任务管理器读数偏差 CPU ≤ 3%、内存 ≤ 2%。
3. 进程页排序/搜索流畅（500 进程下首屏 < 500ms），CPU 排序与任务管理器 Top5 一致。
4. "今天/本月/本年"曲线均可打开，切换 < 1s；本月曲线能看到分钟级峰值。
5. 注入 CPU 压力（100% 持续 6 分钟）后，事件日志出现阈值记录，曲线上有对应事件标记。
6. 面板自身常驻内存 < 100MB、空闲 CPU < 1.5%。
7. 断开浏览器重连后 3s 内恢复实时数据。

---

## 12. 里程碑规划

| 阶段 | 内容 | 周期 |
|------|------|------|
| M1 | 骨架：服务宿主、采集调度、CPU/内存/网络采集、REST+WS、基础仪表盘 | 1.5 周 |
| M2 | 存储：SQLite、三级归档、历史曲线页、事件日志页 | 1.5 周 |
| M3 | 进程：全维度进程采集（含 ETW 网络、GPU 归因）、进程页 | 1.5 周 |
| M4 | 打磨：鉴权、安装包、性能压测、降级策略、文档 | 1 周 |

**v1.1+ 规划**：告警推送（Webhook/邮件）、每进程 GPU 显存曲线、磁盘 IO 曲线、多机纳管、远程结束进程（二次确认 + 审计）。
