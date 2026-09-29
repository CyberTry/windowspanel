import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import { api, type LiveSample, type SummaryDto, type ProcessInfo, type EventDto } from '../api/client';
import { liveSocket } from '../api/ws';

export const usePanelStore = defineStore('panel', () => {
  // 实时数据
  const live       = ref<LiveSample | null>(null);
  // 仪表盘一次性快照（含硬件 / 进程 / 运行时长）
  const summary    = ref<SummaryDto | null>(null);
  // 进程列表（DashboardView 与 ProcessView 共用）
  const processes  = ref<ProcessInfo[]>([]);
  // 事件日志
  const events     = ref<EventDto[]>([]);

  const wsStatus  = ref<'closed' | 'connecting' | 'open'>('closed');

  // 历史曲线环形缓冲（最近 300 帧 ≈ 5 分钟 @ 1Hz）
  const ringSize = 300;
  const cpuRing   = ref<number[]>([]);
  const memRing   = ref<number[]>([]);
  const gpuRing   = ref<number[]>([]);
  const rxRing    = ref<number[]>([]);
  const txRing    = ref<number[]>([]);

  function pushRing(v: number, arr: number[]) {
    arr.push(v);
    if (arr.length > ringSize) arr.splice(0, arr.length - ringSize);
  }

  let off: (() => void) | null = null;

  async function startLive() {
    if (off) return;
    off = liveSocket.onMsg((m) => {
      wsStatus.value = liveSocket.getStatus();
      if (m.type !== 'live' || !m.data) return;
      const s = m.data as LiveSample;
      live.value = s;
      pushRing(s.cpu,    cpuRing.value);
      pushRing(s.memUsedPct, memRing.value);
      pushRing(s.gpu,    gpuRing.value);
      pushRing(s.netRxBps, rxRing.value);
      pushRing(s.netTxBps, txRing.value);
    });
    liveSocket.start();
    await refreshSummary();
  }

  async function refreshSummary() {
    try {
      summary.value = await api.summary();
      processes.value = summary.value.processes;
    } catch { /* 后端未就绪时静默 */ }
  }

  async function loadProcesses(sort: 'cpu' | 'mem' | 'pid' = 'cpu', order: 'asc' | 'desc' = 'desc', q = '') {
    try { processes.value = await api.processes(sort, order, q || undefined); }
    catch { /* ignore */ }
  }

  async function loadEvents(level?: string, limit = 200) {
    try { events.value = await api.events(level, limit); } catch { /* ignore */ }
  }

  const uptimeText = computed(() => {
    if (!summary.value) return '00:00:00';
    return summary.value.uptime;
  });

  return {
    live, summary, processes, events, wsStatus,
    cpuRing, memRing, gpuRing, rxRing, txRing,
    startLive, refreshSummary, loadProcesses, loadEvents, uptimeText
  };
});