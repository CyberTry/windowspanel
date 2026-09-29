<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { api } from '@/api/client';
import { usePanelStore } from '@/stores/panel';
const panel = usePanelStore();
const hostLabel = ref('');
onMounted(async () => {
  try {
    const s = await api.settings();
    hostLabel.value = (s.url || '').replace(/^https?:\/\//, '');
  } catch {
    hostLabel.value = window.location.host;
  }
});
</script>

<template>
  <footer>
    <div class="seg"><b>● {{ panel.wsStatus === 'open' ? 'WS CONNECTED' : 'WS DISCONNECTED' }}</b></div>
    <div class="seg">SRC <span class="y">PDH / ETW</span></div>
    <div class="seg">INTERVAL <span class="y">1s</span></div>
    <div class="seg">READY</div>
    <div class="right">
      <div class="seg">{{ panel.summary?.hardware.os ?? 'WIN' }}</div>
      <div class="seg">{{ hostLabel || '…' }}</div>
    </div>
  </footer>
</template>
