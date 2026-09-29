<script setup lang="ts">
import { usePanelStore } from '@/stores/panel';
const panel = usePanelStore();
</script>

<template>
  <footer>
    <div class="seg"><b>● {{ panel.wsStatus === 'open' ? 'WS CONNECTED' : 'WS DISCONNECTED' }}</b></div>
    <div class="seg">SRC <span class="y">PDH / ETW</span></div>
    <div class="seg">INTERVAL <span class="y">1s</span></div>
    <div class="seg">READY</div>
    <div class="right">
      <div class="seg">{{ panel.summary?.hardware.os ?? 'WIN' }}</div>
      <div class="seg">127.0.0.1:9720</div>
    </div>
  </footer>
</template>