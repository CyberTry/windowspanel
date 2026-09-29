<script setup lang="ts">
import { onMounted } from 'vue';
import { usePanelStore } from './stores/panel';
import AppHeader  from './components/AppHeader.vue';
import AppSidebar from './components/AppSidebar.vue';
import AppFooter  from './components/AppFooter.vue';

const panel = usePanelStore();
onMounted(() => panel.startLive());
</script>

<template>
  <AppHeader />
  <AppSidebar />
  <main>
    <router-view />
  </main>
  <AppFooter />
</template>