<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed } from 'vue';
import { useRoute } from 'vue-router';
import { usePanelStore } from '@/stores/panel';

const route  = useRoute();
const panel  = usePanelStore();

const titles = computed<[string, string]>(() => {
  const t = route.meta.title as [string, string] | undefined;
  return t ?? ['仪表盘', 'DASHBOARD · REALTIME OVERVIEW'];
});

const clock = ref('');
let interval: number;
onMounted(() => {
  const tick = () => { clock.value = new Date().toTimeString().slice(0, 8); };
  tick(); interval = window.setInterval(tick, 1000);
});
onUnmounted(() => clearInterval(interval));

const wsLabel = computed(() => panel.wsStatus === 'open' ? 'WS LIVE' : 'WS OFF');
</script>

<template>
  <div class="hazard" />
  <header>
    <div class="logo">WP</div>
    <div class="page-title">
      <span class="title-mark" />
      <h1>{{ titles[0] }}</h1>
      <span class="en">{{ titles[1] }}</span>
    </div>
    <div class="spacer" />
    <div class="top-chips">
      <div class="chip" :class="{ 'ws-off': panel.wsStatus !== 'open' }">
        <span class="dot" />{{ wsLabel }}
      </div>
      <div class="chip">HOST <b>{{ panel.summary?.hardware.hostName ?? '--' }}</b></div>
      <div class="chip">UPTIME <b>{{ panel.uptimeText }}</b></div>
      <div class="chip clock"><span class="dot" />{{ clock }}</div>
    </div>
  </header>
</template>