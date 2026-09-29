import { createRouter, createWebHashHistory, type RouteRecordRaw } from 'vue-router';

const routes: RouteRecordRaw[] = [
  { path: '/',          redirect: '/dashboard' },
  { path: '/dashboard', component: () => import('../views/DashboardView.vue'), meta: { title: ['仪表盘', 'DASHBOARD · REALTIME OVERVIEW'] } },
  { path: '/processes', component: () => import('../views/ProcessView.vue'),   meta: { title: ['进程',     'PROCESSES · TASK MANAGER VIEW'] } },
  { path: '/history',   component: () => import('../views/HistoryView.vue'),   meta: { title: ['历史曲线', 'HISTORY · LOAD CURVES (1D / 1M / 1Y)'] } },
  { path: '/logs',      component: () => import('../views/LogsView.vue'),      meta: { title: ['事件日志', 'EVENT LOG · ALERTS & AUDIT'] } }
];

export const router = createRouter({
  history: createWebHashHistory(),
  routes
});