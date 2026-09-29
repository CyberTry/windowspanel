<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue';
import * as echarts from 'echarts/core';
import { LineChart } from 'echarts/charts';
import { GridComponent, TooltipComponent, LegendComponent, DataZoomComponent } from 'echarts/components';
import { CanvasRenderer } from 'echarts/renderers';
import { api, type HistoryDto } from '@/api/client';

echarts.use([LineChart, GridComponent, TooltipComponent, LegendComponent, DataZoomComponent, CanvasRenderer]);

type MetricKey = 'cpu' | 'mem' | 'gpu' | 'vram' | 'rx' | 'tx';

const range  = ref<'1d' | '1m' | '1y'>('1d');
const metrics = ref<Record<MetricKey, boolean>>({ cpu: true, mem: true, gpu: true, vram: false, tx: false, rx: true });
const data   = ref<HistoryDto | null>(null);
const chartRef = ref<HTMLDivElement>();
let inst: echarts.ECharts | null = null;
let ro: ResizeObserver | null = null;

const colorMap: Record<MetricKey, string> = {
  cpu: '#FF9F1C', mem: '#FFE600', gpu: '#3EC6FF',
  vram: '#B57BFF', rx: '#37E86B', tx: '#FF6B9D'
};
const labelMap: Record<MetricKey, string> = {
  cpu: 'CPU', mem: '内存', gpu: 'GPU', vram: '显存', rx: '下行', tx: '上行'
};
const metricToApi: Record<MetricKey, string> = {
  cpu: 'cpu_total', mem: 'mem_used', gpu: 'gpu_util',
  vram: 'vram_used', rx: 'net_rx', tx: 'net_tx'
};

async function reload() {
  const enabled = (Object.keys(metrics.value) as MetricKey[]).filter(k => metrics.value[k]);
  const m = enabled.map(k => metricToApi[k]).join(',');
  data.value = await api.history(m, range.value);
  updateChart();
}

function metricKeyFromApi(m: string): MetricKey {
  const map: Record<string, MetricKey> = {
    cpu_total: 'cpu', mem_used: 'mem', gpu_util: 'gpu',
    vram_used: 'vram', net_rx: 'rx', net_tx: 'tx'
  };
  return map[m] ?? 'cpu';
}

function updateChart() {
  if (!inst || !data.value) return;
  const series = data.value.series.map(s => {
    const k = metricKeyFromApi(s.metric);
    return {
        name: labelMap[k],
        type: 'line',
        smooth: true,
        symbol: 'none',
        lineStyle: { color: colorMap[k], width: 1.5 },
        areaStyle: { color: colorMap[k] + '22' },
        data: s.points.map(p => [p.ts, p.value])
      };
  });
  inst.setOption({
    grid: { left: 50, right: 24, top: 30, bottom: 56 },
    legend: { textStyle: { color: '#EDEDE3', fontSize: 11 }, top: 4 },
    tooltip: {
      trigger: 'axis',
      backgroundColor: 'rgba(0,0,0,.92)',
      borderColor: '#FFE600',
      textStyle: { color: '#FFE600', fontSize: 11 },
      axisPointer: { lineStyle: { color: 'rgba(255,230,0,.5)' } }
    },
    xAxis: {
      type: 'time',
      axisLine: { lineStyle: { color: '#2C2C22' } },
      axisLabel: { color: '#8A8A78', fontSize: 9 },
      splitLine: { lineStyle: { color: 'rgba(255,255,255,.05)' } }
    },
    yAxis: {
      type: 'value',
      axisLine: { lineStyle: { color: '#2C2C22' } },
      axisLabel: { color: '#8A8A78', fontSize: 9, formatter: '{value}%' },
      splitLine: { lineStyle: { color: 'rgba(255,255,255,.05)' } }
    },
    dataZoom: [
      { type: 'inside', start: 0, end: 100 },
      { type: 'slider', height: 18, bottom: 12, borderColor: '#2C2C22', backgroundColor: 'transparent',
        fillerColor: 'rgba(255,230,0,.15)', handleStyle: { color: '#FFE600' }, textStyle: { color: '#8A8A78', fontSize: 9 } }
    ],
    series
  });
}

const tabs: { key: '1d' | '1m' | '1y'; label: string }[] = [
  { key: '1d', label: '今天 · 1天' },
  { key: '1m', label: '本月 · 1月' },
  { key: '1y', label: '本年 · 1年' }
];

onMounted(async () => {
  if (chartRef.value) inst = echarts.init(chartRef.value, undefined, { renderer: 'canvas' });
  await reload();
  ro = new ResizeObserver(() => inst?.resize());
  if (chartRef.value) ro.observe(chartRef.value);
});

onUnmounted(() => { inst?.dispose(); ro?.disconnect(); });
</script>

<template>
  <section class="page">
    <div class="toolbar">
      <button v-for="t in tabs" :key="t.key" class="btn" :class="{ on: range === t.key }" @click="range = t.key; reload()">{{ t.label }}</button>
      <div class="chip">STEP <b>{{ (data?.step ?? 'raw').toUpperCase() }}</b></div>
      <div class="spacer" />
      <button class="btn">导出 CSV</button>
    </div>

    <div class="metric-chips">
      <span v-for="m in (Object.keys(metrics) as MetricKey[])" :key="m"
            class="mchip"
            :class="{ on: metrics[m] }"
            :style="{ '--mc': colorMap[m] }"
            @click="metrics[m] = !metrics[m]; reload()">
        <i></i>{{ labelMap[m] }}
      </span>
    </div>

    <div class="panel">
      <div class="p-head">
        <span class="tri"></span><h2>负载曲线</h2>
        <span class="en">{{ range.toUpperCase() }} · {{ data?.series[0]?.points.length ?? 0 }} pts</span>
        <div class="tools"><span class="badge warn">采样 · 5S RAW</span></div>
      </div>
      <div class="p-body"><div ref="chartRef" class="chart" /></div>
      <div class="corner" />
    </div>
  </section>
</template>

<style scoped>
.page { padding: 14px 16px; height: 100%; overflow-y: auto; }
.toolbar { display: flex; gap: 8px; align-items: center; margin-bottom: 12px; flex-wrap: wrap; }
.btn { border: 1px solid var(--line); background: var(--panel); color: var(--txt); padding: 6px 16px; font-size: 11px; letter-spacing: 1px; cursor: pointer; clip-path: var(--clip-tag); transition: all .22s ease; }
.btn:hover { border-color: var(--yellow-dim); color: var(--yellow); }
.btn.on { background: var(--yellow); color: #000; border-color: var(--yellow); font-weight: 700; }
.chip { border: 1px solid var(--line); background: var(--panel); padding: 4px 10px; font-size: 11px; letter-spacing: 1px; clip-path: var(--clip-tag); }
.chip b { color: var(--yellow); font-weight: 700; }
.spacer { flex: 1; }

.metric-chips { display: flex; gap: 6px; flex-wrap: wrap; margin-bottom: 12px; }
.mchip { border: 1px solid var(--line); padding: 5px 12px; font-size: 11px; cursor: pointer; clip-path: var(--clip-tag); color: var(--txt-dim); transition: all .22s ease; }
.mchip i { display: inline-block; width: 8px; height: 8px; margin-right: 6px; background: var(--mc); }
.mchip.on { color: var(--txt); border-color: var(--mc); background: color-mix(in srgb, var(--mc) 12%, transparent); }

.chart { width: 100%; height: 360px; }
</style>