<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue';
import * as echarts from 'echarts/core';
import { LineChart } from 'echarts/charts';
import { GridComponent, TooltipComponent } from 'echarts/components';
import { CanvasRenderer } from 'echarts/renderers';
import { usePanelStore } from '@/stores/panel';
import MetricCard from '@/components/MetricCard.vue';
import Gauge     from '@/components/Gauge.vue';

echarts.use([LineChart, GridComponent, TooltipComponent, CanvasRenderer]);
const panel = usePanelStore();

const mainRef = ref<HTMLDivElement>();
const netRef  = ref<HTMLDivElement>();
let mainInst: echarts.ECharts | null = null;
let netInst:  echarts.ECharts | null = null;
let ro: ResizeObserver | null = null;

const yellow = '#FFE600', orange = '#FF9F1C', cyan = '#3EC6FF';

function axis() {
  return {
    axisLine:  { lineStyle: { color: '#2C2C22' } },
    axisLabel: { color: '#8A8A78', fontSize: 9, fontFamily: 'Consolas, monospace' },
    splitLine: { lineStyle: { color: 'rgba(255,255,255,.05)' } }
  };
}

function buildMainOption() {
  const xs = panel.cpuRing.map((_, i) => -panel.cpuRing.length + i + 1);
  return {
    grid: { left: 38, right: 16, top: 10, bottom: 22 },
    tooltip: {
      trigger: 'axis',
      backgroundColor: 'rgba(0,0,0,.92)',
      borderColor: yellow,
      textStyle: { color: yellow, fontSize: 11 },
      formatter: (params: any[]) => {
        const sec = -params[0].axisValue;
        return `<b style="color:#8A8A78">T-${sec}s</b><br>` +
          params.map(p => `<span style="color:${p.color}">●</span> ${p.seriesName} <b>${(+p.value[1]).toFixed(1)}%</b>`).join('<br>');
      }
    },
    xAxis: { type: 'value', ...axis(), min: -300, max: 0 },
    yAxis: { type: 'value', ...axis(), min: 0, max: 100, axisLabel: { ...axis().axisLabel, formatter: '{value}%' } },
    series: [
      line('CPU', panel.cpuRing, orange, xs),
      line('MEM', panel.memRing, yellow, xs),
      line('GPU', panel.gpuRing, cyan,   xs)
    ]
  };
}

function buildNetOption() {
  const xs = panel.rxRing.map((_, i) => -panel.rxRing.length + i + 1);
  return {
    grid: { left: 50, right: 16, top: 10, bottom: 22 },
    tooltip: { trigger: 'axis', backgroundColor: 'rgba(0,0,0,.92)', borderColor: yellow, textStyle: { color: yellow, fontSize: 11 } },
    xAxis: { type: 'value', ...axis(), min: -300, max: 0 },
    yAxis: { type: 'value', ...axis(), axisLabel: { ...axis().axisLabel, formatter: (v: number) => `${v.toFixed(0)} KB` } },
    series: [
      line('RX ↓', panel.rxRing.map(v => v / 1024), cyan,   xs),
      line('TX ↑', panel.txRing.map(v => v / 1024), orange, xs)
    ]
  };
}

function line(name: string, data: number[], color: string, xs: number[]) {
  return {
    name, type: 'line', smooth: true, symbol: 'none',
    lineStyle: { color, width: 1.5 },
    areaStyle: { color: color + '22' },
    data: xs.map((x, i) => [x, data[i] ?? 0])
  };
}

onMounted(() => {
  if (mainRef.value) mainInst = echarts.init(mainRef.value, undefined, { renderer: 'canvas' });
  if (netRef.value)  netInst  = echarts.init(netRef.value,  undefined, { renderer: 'canvas' });
  let raf = 0;
  const tick = () => {
    mainInst?.setOption(buildMainOption(), false, true);
    netInst?.setOption(buildNetOption(), false, true);
    raf = requestAnimationFrame(tick);
  };
  tick();

  ro = new ResizeObserver(() => { mainInst?.resize(); netInst?.resize(); });
  if (mainRef.value) ro.observe(mainRef.value);
  if (netRef.value)  ro.observe(netRef.value);
  onUnmounted(() => cancelAnimationFrame(raf));
});

onUnmounted(() => { mainInst?.dispose(); netInst?.dispose(); ro?.disconnect(); });

const memFmt = (b: number) => b ? `${(b / (1 << 30)).toFixed(1)} GB` : 'n/a';
</script>

<template>
  <section class="page">
    <!-- 6 metric cards -->
    <div class="cards">
      <MetricCard title="CPU 使用率" tag="CPU"    unit="%"    source="cpu"  ringKey="cpuRing" sub="16 LOGICAL · 2.30GHZ" />
      <MetricCard title="内存占用"   tag="MEMORY" unit="%"    source="mem"  ringKey="memRing" sub="15.0 GB / 32.0 GB" />
      <MetricCard title="GPU 使用率" tag="GPU"    unit="%"    source="gpu"  ringKey="gpuRing" sub="RTX 3060 · ENGINE SUM" />
      <MetricCard title="显存占用"   tag="VRAM"   unit="%"    source="vram" ringKey="gpuRing" sub="3.8 GB / 12.0 GB" />
      <MetricCard title="上行速率"   tag="TX"     unit="KB/s" source="tx"   ringKey="txRing"  cyan />
      <MetricCard title="下行速率"   tag="RX"     unit="KB/s" source="rx"   ringKey="rxRing"  cyan />
    </div>

    <div class="dash-grid">
      <div class="dash-col">
        <div class="panel">
          <div class="p-head">
            <span class="tri"></span><h2>实时负载曲线</h2><span class="en">REALTIME LOAD</span>
            <div class="tools legend">
              <span><i style="background:var(--orange)"></i>CPU</span>
              <span><i style="background:var(--yellow)"></i>MEM</span>
              <span><i style="background:var(--cyan)"></i>GPU</span>
              <span class="badge warn">1s</span>
            </div>
          </div>
          <div class="p-body"><div ref="mainRef" class="chart main-chart" /></div>
          <div class="corner" />
        </div>

        <div class="panel">
          <div class="p-head">
            <span class="tri"></span><h2>网络吞吐</h2><span class="en">NETWORK I/O</span>
            <div class="tools legend">
              <span><i style="background:var(--cyan)"></i>下行</span>
              <span><i style="background:var(--orange)"></i>上行</span>
            </div>
          </div>
          <div class="p-body"><div ref="netRef" class="chart net-chart" /></div>
          <div class="corner" />
        </div>
      </div>

      <div class="dash-col">
        <div class="panel">
          <div class="p-head"><span class="tri"></span><h2>仪表</h2><span class="en">GAUGES</span></div>
          <div class="p-body gauges">
            <div class="gauge">
              <Gauge :value="panel.live?.cpu ?? 0" color="#FFE600" />
              <div class="g-label">CPU LOAD</div>
            </div>
            <div class="gauge">
              <Gauge :value="panel.live?.gpu ?? 0" color="#3EC6FF" />
              <div class="g-label">GPU LOAD</div>
            </div>
          </div>
        </div>

        <div class="panel">
          <div class="p-head"><span class="tri"></span><h2>硬件信息</h2><span class="en">HARDWARE</span></div>
          <div class="p-body">
            <table class="kv">
              <tr><td>操作系统</td><td>{{ panel.summary?.hardware.os ?? '--' }}</td></tr>
              <tr><td>处理器</td><td>{{ panel.summary?.hardware.cpu ?? '--' }}</td></tr>
              <tr><td>内存</td><td>{{ memFmt(panel.summary?.hardware.memTotalBytes ?? 0) }}</td></tr>
              <tr><td>显卡</td><td>{{ panel.summary?.hardware.gpu ?? '--' }}</td></tr>
              <tr><td>磁盘</td><td>{{ panel.summary?.hardware.disk ?? '--' }}</td></tr>
              <tr><td>采样</td><td><span class="y">1s / 5s</span> · 3 张</td></tr>
            </table>
          </div>
        </div>

        <div class="panel">
          <div class="p-head"><span class="tri"></span><h2>实时概览</h2><span class="en">OVERVIEW</span></div>
          <div class="p-body">
            <div class="summary-row"><span class="k">进程数</span><span class="v">{{ panel.processes.length }}</span></div>
            <div class="summary-row"><span class="k">CPU 当前</span><span class="v">{{ (panel.live?.cpu ?? 0).toFixed(1) }}%</span></div>
            <div class="summary-row"><span class="k">GPU 当前</span><span class="v">{{ (panel.live?.gpu ?? 0).toFixed(1) }}%</span></div>
            <div class="summary-row"><span class="k">内存当前</span><span class="v">{{ (panel.live?.memUsedPct ?? 0).toFixed(1) }}%</span></div>
            <div class="summary-row"><span class="k">RX / TX</span><span class="v">{{ ((panel.live?.netRxBps ?? 0)/1024).toFixed(1) }} / {{ ((panel.live?.netTxBps ?? 0)/1024).toFixed(1) }} KB/s</span></div>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>

<style scoped>
.page { padding: 14px 16px; height: 100%; overflow-y: auto; }
.cards { display: grid; grid-template-columns: repeat(6, 1fr); gap: 12px; margin-bottom: 12px; }
.dash-grid { display: grid; grid-template-columns: 1fr 296px; gap: 12px; }
.dash-col { min-width: 0; }
.chart { width: 100%; }
.main-chart { height: 300px; }
.net-chart  { height: 180px; }
.gauges { display: flex; justify-content: space-around; padding: 8px 0 4px; }
.gauge { text-align: center; }
.gauge .g-label { font-size: 9px; color: var(--txt-dim); letter-spacing: 2px; }

.kv { width: 100%; border-collapse: collapse; font-size: 11px; }
.kv td { padding: 6px 4px; border-bottom: 1px dashed var(--line); }
.kv tr:last-child td { border-bottom: none; }
.kv td:first-child { color: var(--txt-dim); }
.kv td:last-child { text-align: right; font-family: Consolas, monospace; color: var(--txt); }
.kv td:last-child .y { color: var(--yellow); }

.summary-row { display: flex; justify-content: space-between; padding: 7px 4px; border-bottom: 1px dashed var(--line); font-size: 11px; }
.summary-row:last-child { border-bottom: none; }
.summary-row .k { color: var(--txt-dim); }
.summary-row .v { font-family: Consolas, monospace; }

.legend { display: flex; gap: 14px; font-size: 10px; color: var(--txt-dim); }
.legend i { display: inline-block; width: 14px; height: 3px; margin-right: 5px; vertical-align: middle; }
</style>