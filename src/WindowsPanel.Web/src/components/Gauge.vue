<script setup lang="ts">
import { ref, onMounted, onUnmounted, watch } from 'vue';
const props = withDefaults(defineProps<{ value: number; color?: string; label?: string }>(), { label: '' });
const cv = ref<HTMLCanvasElement>();
let raf = 0;

function draw() {
  if (!cv.value) return;
  const c = cv.value;
  const ctx = c.getContext('2d');
  if (!ctx) return;
  const w = c.width, h = c.height;
  ctx.clearRect(0, 0, w, h);
  const cx = w / 2, cy = h / 2 + 6, R = 44;
  const st = Math.PI * 0.75, en = Math.PI * 2.25;
  const color = props.color ?? '#FFE600';

  ctx.lineWidth = 9; ctx.lineCap = 'butt';
  ctx.strokeStyle = '#242420';
  ctx.beginPath(); ctx.arc(cx, cy, R, st, en); ctx.stroke();

  ctx.strokeStyle = color;
  ctx.shadowColor = color; ctx.shadowBlur = 10;
  ctx.beginPath();
  ctx.arc(cx, cy, R, st, st + (en - st) * Math.max(0, Math.min(100, props.value)) / 100);
  ctx.stroke(); ctx.shadowBlur = 0;

  // ticks
  ctx.strokeStyle = '#3a3a30'; ctx.lineWidth = 1;
  for (let i = 0; i <= 10; i++) {
    const a = st + (en - st) * i / 10;
    ctx.beginPath();
    ctx.moveTo(cx + Math.cos(a) * (R - 10), cy + Math.sin(a) * (R - 10));
    ctx.lineTo(cx + Math.cos(a) * (R - 15), cy + Math.sin(a) * (R - 15));
    ctx.stroke();
  }

  // value text
  ctx.fillStyle = color;
  ctx.font = 'bold 20px Consolas, monospace';
  ctx.textAlign = 'center';
  ctx.fillText(props.value.toFixed(1), cx, cy + 2);
  ctx.fillStyle = '#8A8A78';
  ctx.font = '9px Consolas, monospace';
  ctx.fillText('%', cx, cy + 16);
}

onMounted(() => {
  raf = requestAnimationFrame(function loop() { draw(); raf = requestAnimationFrame(loop); });
});
onUnmounted(() => cancelAnimationFrame(raf));
watch(() => props.value, () => draw());
</script>

<template>
  <canvas ref="cv" width="120" height="110"></canvas>
</template>