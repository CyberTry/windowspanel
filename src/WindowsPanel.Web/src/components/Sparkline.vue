<script setup lang="ts">
import { ref, onMounted, onUnmounted, watch } from 'vue';

const props = withDefaults(defineProps<{
  series: number[];
  color?: string;
  height?: number;
  max?: number;
}>(), { height: 34 });

const cv = ref<HTMLCanvasElement>();
let raf = 0;

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
  const color = props.color ?? '#FFE600';

  // gradient fill
  const grad = ctx.createLinearGradient(0, 0, 0, h);
  grad.addColorStop(0, color + '55');
  grad.addColorStop(1, color + '00');
  ctx.beginPath();
  data.forEach((v, i) => {
    const x = i / (data.length - 1) * w;
    const y = h - ((v - min) / Math.max(1, max - min)) * h;
    i ? ctx.lineTo(x, y) : ctx.moveTo(x, y);
  });
  ctx.lineTo(w, h); ctx.lineTo(0, h); ctx.closePath();
  ctx.fillStyle = grad; ctx.fill();

  // line
  ctx.beginPath();
  data.forEach((v, i) => {
    const x = i / (data.length - 1) * w;
    const y = h - ((v - min) / Math.max(1, max - min)) * h;
    i ? ctx.lineTo(x, y) : ctx.moveTo(x, y);
  });
  ctx.lineWidth = 1.5; ctx.strokeStyle = color; ctx.stroke();

  // endpoint dot
  const last = data[data.length - 1];
  const ly = h - ((last - min) / Math.max(1, max - min)) * h;
  ctx.fillStyle = color; ctx.fillRect(w - 4, ly - 1.5, 4, 3);
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