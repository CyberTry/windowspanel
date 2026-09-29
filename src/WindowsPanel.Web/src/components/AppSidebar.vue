<script setup lang="ts">
import { useRouter, useRoute } from 'vue-router';
const router = useRouter();
const route  = useRoute();

const items = [
  { to: '/dashboard', icon: '▤', label: '仪表盘' },
  { to: '/processes', icon: '☰', label: '进程'   },
  { to: '/history',   icon: '◧', label: '历史'   },
  { to: '/logs',      icon: '❑', label: '日志'   }
];

function go(to: string) { router.push(to); }
</script>

<template>
  <nav>
    <div
      v-for="it in items" :key="it.to"
      class="nav-btn"
      :class="{ active: route.path === it.to }"
      @click="go(it.to)"
    >
      <span class="ico">{{ it.icon }}</span>
      <span class="lb">{{ it.label }}</span>
    </div>
    <div class="ver">v1.0<br>WP·01</div>
  </nav>
</template>