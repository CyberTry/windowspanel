<script setup lang="ts">
import { ref, onMounted, onUnmounted, watch } from 'vue';

const props = withDefaults(defineProps<{
  series: number[];
  color?: string;
  height?: number;
  max?: number;
}>(), { height: 34, color: '#FFE600' });

const cv = ref<HTMLCanvasElement>();
let raf = 0;

// 接受 hex (#RGB / #RRGGBB) 或 rgb()/rgba() 或命名色；与 alpha 输出 span 合成 rgba 字符串
function withAlpha(input: string, alpha: number): string {
  const c = (input || '').trim();
  // hex
  const m = /^#([0-9a-f]{3,8})$/i.exec(c);
  if (m) {
    let hex = m[1];
    if (hex.length === 3) hex = hex.split('').map(ch => ch + ch).join('');
    const r = parseInt(hex.slice(0, 2), 16);
    const g = parseInt(hex.slice(2, 4), 16);
    const b = parseInt(hex.slice(4, 6), 16);
    return `rgba(${r},${g},${b},${alpha})`;
  }
  // rgb(...) → 转为 rgba
  const rgb = /^rgb\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)$/i.exec(c);
  if (rgb) return `rgba(${rgb[1]},${rgb[2]},${rgb[3]},${alpha})`;
  // 已是 rgba(...) → 替换 alpha
  const rgba = /^rgba\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*[\d.]+\s*\)$/i.exec(c);
  if (rgba) return `rgba(${rgba[1]},${rgba[2]},${rgba[3]},${alpha})`;
  // CSS var 或未知格式 → 用 globalAlpha 兜底
  return c;
}

function draw() {
  if (!cv.value) return;
  const c = cv.value;
  const ctx = c.getContext('2d');
  if (!ctx) return;
  const dpr = window.devicePixelRatio || 1;
  const r = c.getBoundingClientRect();
  if (r.width === 0) return;
  c.width  = r.width * dpr;
  c.height = props.height * dpr;
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  const w = r.width, h = props.height;
  ctx.clearRect(0, 0, w, h);

  const data = props.series;
  if (data.length < 2) return;
  const max = props.max ?? Math.max(...data, 1);
  const min = 0;
  const color = props.color;
  const isCssVar = /^var\(/i.test(color);

  // gradient fill
  const grad = ctx.createLinearGradient(0, 0, 0, h);
  if (isCssVar) {
    // 兜底：用全局 alpha + 实色覆盖的方案
    grad.addColorStop(0, withAlpha('#000000', 0));
    grad.addColorStop(1, withAlpha('#000000', 0));
  } else {
    grad.addColorStop(0, withAlpha(color, 0.33));
    grad.addColorStop(1, withAlpha(color, 0.0));
  }
  ctx.beginPath();
  data.forEach((v, i) => {
    const x = i / (data.length - 1) * w;
    const y = h - ((v - min) / Math.max(1, max - min)) * h;
    i ? ctx.lineTo(x, y) : ctx.moveTo(x, y);
  });
  ctx.lineTo(w, h); ctx.lineTo(0, h); ctx.closePath();
  if (!isCssVar) {
    ctx.fillStyle = grad; ctx.fill();
  } else {
    // 用 currentColor 取 CSS 变量实际值（来自元素的 color CSS 属性）
    const resolved = getComputedStyle(c).color || 'rgb(255,255,255)';
    const flat = ctx.createLinearGradient(0, 0, 0, h);
    flat.addColorStop(0, withAlpha(resolved, 0.33));
    flat.addColorStop(1, withAlpha(resolved, 0.0));
    ctx.fillStyle = flat; ctx.fill();
  }

  // line
  ctx.beginPath();
  data.forEach((v, i) => {
    const x = i / (data.length - 1) * w;
    const y = h - ((v - min) / Math.max(1, max - min)) * h;
    i ? ctx.lineTo(x, y) : ctx.moveTo(x, y);
  });
  ctx.lineWidth = 1.5;
  ctx.strokeStyle = isCssVar ? (getComputedStyle(c).color || '#FFE600') : color;
  ctx.stroke();

  // endpoint dot
  const last = data[data.length - 1];
  const ly = h - ((last - min) / Math.max(1, max - min)) * h;
  ctx.fillStyle = ctx.strokeStyle as string;
  ctx.fillRect(w - 4, ly - 1.5, 4, 3);
}

onMounted(() => {
  raf = requestAnimationFrame(function loop() { draw(); raf = requestAnimationFrame(loop); });
});
onUnmounted(() => cancelAnimationFrame(raf));
watch(() => props.series, () => draw(), { deep: true });
</script>

<template>
  <canvas ref="cv" :style="{ height: height + 'px' }"></canvas>
</template>

<style scoped>
canvas { width: 100%; display: block; }
</style>