// 轻量 REST 客户端。所有端点以前缀 /api/v1 暴露在同一个 origin（由 vite proxy 转发到 .NET Server）。

const base = '/api/v1';

async function get<T>(path: string, params?: Record<string, string | number | undefined>): Promise<T> {
  const url = new URL(base + path, location.origin);
  if (params) for (const [k, v] of Object.entries(params)) if (v !== undefined) url.searchParams.set(k, String(v));
  const r = await fetch(url.toString());
  if (!r.ok) throw new Error(`${r.status} ${r.statusText}`);
  return r.json();
}

export interface LiveSample {
  ts: number;
  cpu: number;
  memUsedPct: number; memUsedBytes: number; memTotalBytes: number;
  gpu: number;
  vramUsedPct: number; vramUsedBytes: number; vramTotalBytes: number;
  netTxBps: number; netRxBps: number;
}
export interface ProcessInfo { pid: number; name: string; cpu: number; memBytes: number; memFmt: string; diskRead: number; diskWrite: number; netTx: number; netRx: number; gpu: number; status: string; }
export interface HardwareInfo { os: string; cpu: string; cores: number; memTotalBytes: number; gpu: string; vramTotalBytes: number; disk: string; hostName: string; uptime: string; }
export interface SummaryDto  { live: LiveSample; processes: ProcessInfo[]; hardware: HardwareInfo; uptime: string; startedAt: string; }
export interface HistoryDto  { step: string; series: { metric: string; points: { ts: number; value: number }[] }[]; events: { ts: number; label: string }[]; }
export interface EventDto    { id: number; ts: number; level: string; category: string; metric?: string; value?: number; message: string; }
export interface SettingsDto { url: string; authEnabled: boolean; liveSeconds: number; persistSeconds: number; cpuPercent: number; cpuMinutes: number; memAvailPercent: number; }

export const api = {
  summary:   () => get<SummaryDto>('/summary'),
  processes: (sort = 'cpu', order = 'desc', q?: string) =>
    get<ProcessInfo[]>('/processes', { sort, order, q }),
  history:   (metrics: string, range: '1d' | '1m' | '1y' = '1d') =>
    get<HistoryDto>('/history', { metrics, range }),
  events:    (level?: string, limit = 200) =>
    get<EventDto[]>('/events', { level, limit }),
  settings:  () => get<SettingsDto>('/settings')
};