<script setup lang="ts">
import { computed } from 'vue';
import Sparkline from './Sparkline.vue';
import { usePanelStore } from '@/stores/panel';

const props = withDefaults(defineProps<{
  title: string;
  tag: string;
  unit: string;
  /** 实时数值来源键（live 字段名） */
  source: 'cpu' | 'mem' | 'gpu' | 'vram' | 'tx' | 'rx';
  /** 对应 store 中的环形数组（用于 sparkline） */
  ringKey: 'cpuRing' | 'memRing' | 'gpuRing' | 'rxRing' | 'txRing';
  /** sparkline 数值映射（KB/s 或 %） */
  ringScale?: number;
  sub?: string;
  cyan?: boolean;
  decimals?: number;
}>(), { cyan: false, decimals: 1, ringScale: 1, sub: '' });

const panel = usePanelStore();

const value = computed(() => {
  const s = panel.live;
  if (!s) return 0;
  return ({
    cpu:  s.cpu,
    mem:  s.memUsedPct,
    gpu:  s.gpu,
    vram: s.vramUsedPct,
    tx:   s.netTxBps / 1024,
    rx:   s.netRxBps / 1024
  } as Record<string, number>)[props.source] ?? 0;
});

const series = computed(() =>
  (panel[props.ringKey] as number[]).map(v => v * props.ringScale)
);

const formatted = computed(() =>
  `${value.value.toFixed(props.decimals)}<small>${props.unit}</small>`
);
</script>

<template>
  <div class="card" :class="{ tx: cyan }">
    <div class="c-top">
      <span class="c-name"><span class="tri"></span>{{ title }}</span>
      <span class="c-tag">{{ tag }}</span>
    </div>
    <div class="c-val" v-html="formatted" />
    <div class="c-sub">{{ sub }}</div>
    <Sparkline :series="series" :color="cyan ? 'var(--cyan)' : 'var(--yellow)'" />
  </div>
</template>

<style scoped>
.card {
  background: var(--panel);
  border: 1px solid var(--line);
  clip-path: var(--clip-cut);
  padding: 10px 12px 8px;
  position: relative;
  overflow: hidden;
  transition: transform .28s cubic-bezier(.2, .7, .2, 1), border-color .25s, box-shadow .28s;
  cursor: pointer;
}
.card:hover { transform: translateY(-3px); border-color: rgba(255, 230, 0, .4); box-shadow: 0 8px 22px rgba(0, 0, 0, .45), 0 0 0 1px rgba(255, 230, 0, .18), 0 0 18px rgba(255, 230, 0, .08); }
.card .c-top { display: flex; justify-content: space-between; align-items: center; }
.card .c-name { font-size: 11px; color: var(--txt-dim); display: flex; align-items: center; gap: 6px; }
.card .c-name .tri { width: 0; height: 0; border-left: 6px solid var(--yellow); border-top: 4px solid transparent; border-bottom: 4px solid transparent; }
.card .c-tag { font-size: 9px; color: var(--txt-dim); letter-spacing: 1px; border: 1px solid var(--line); padding: 1px 5px; }
.card .c-val { font-size: 26px; font-weight: 800; font-family: Consolas, monospace; margin: 2px 0; color: var(--yellow); text-shadow: 0 0 12px var(--yellow-glow); }
.card .c-val small { font-size: 11px; color: var(--txt-dim); font-weight: 400; margin-left: 3px; }
.card .c-sub { font-size: 9px; color: var(--txt-dim); letter-spacing: 1px; margin-bottom: 6px; }
.card.tx .c-val { color: var(--cyan); text-shadow: 0 0 12px rgba(62, 198, 255, .3); }
.card.tx .c-name .tri { border-left-color: var(--cyan); }
</style>