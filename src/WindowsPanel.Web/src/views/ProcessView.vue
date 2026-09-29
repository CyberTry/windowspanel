<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { usePanelStore } from '@/stores/panel';

const panel = usePanelStore();
const sort = ref<'cpu' | 'mem' | 'pid'>('cpu');
const order = ref<'asc' | 'desc'>('desc');
const q = ref('');

async function reload() {
  await panel.loadProcesses(sort.value, order.value, q.value);
}

function setSort(c: 'cpu' | 'mem' | 'pid') {
  if (sort.value === c) order.value = order.value === 'asc' ? 'desc' : 'asc';
  else { sort.value = c; order.value = 'desc'; }
  reload();
}

onMounted(reload);
</script>

<template>
  <section class="page">
    <div class="toolbar">
      <div class="search"><span>⌕</span><input v-model="q" @keyup.enter="reload" placeholder="搜索进程 / PID…" /></div>
      <button class="btn">应用视图</button>
      <button class="btn on">详细信息</button>
      <div class="spacer" />
      <span class="badge warn">{{ panel.processes.length }} PROCS</span>
    </div>

    <div class="panel">
      <div class="p-head">
        <span class="tri"></span><h2>进程列表</h2>
        <span class="en">PROCESSES · TASK-MANAGER VIEW</span>
      </div>
      <div class="p-body" style="padding:0">
        <table class="stat-table">
          <thead>
            <tr>
              <th @click="setSort('pid')">进程名 / PID</th>
              <th @click="setSort('cpu')">CPU %</th>
              <th @click="setSort('mem')">内存</th>
              <th>磁盘</th>
              <th>网络</th>
              <th>GPU</th>
              <th>状态</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="p in panel.processes" :key="p.pid">
              <td>
                <span class="mname">
                  <i style="background:var(--yellow)"></i>{{ p.name }}
                  <span style="color:var(--txt-dim);font-size:10px">#{{ p.pid }}</span>
                </span>
              </td>
              <td :class="{ hot: p.cpu > 20 }">{{ p.cpu.toFixed(1) }}%</td>
              <td>{{ p.memFmt }}</td>
              <td>0</td>
              <td>0</td>
              <td>{{ p.gpu.toFixed(1) }}%</td>
              <td style="color:var(--green);font-family:'Segoe UI',sans-serif">● {{ p.status }}</td>
            </tr>
          </tbody>
        </table>
      </div>
      <div class="corner" />
    </div>
  </section>
</template>

<style scoped>
.page { padding: 14px 16px; height: 100%; overflow-y: auto; }
.toolbar { display: flex; gap: 8px; align-items: center; margin-bottom: 12px; flex-wrap: wrap; }
.search { display: flex; align-items: center; gap: 6px; border: 1px solid var(--line); background: var(--panel); padding: 5px 10px; clip-path: var(--clip-tag); }
.search input { background: none; border: none; outline: none; color: var(--txt); font-size: 11px; width: 180px; }
.btn { border: 1px solid var(--line); background: var(--panel); color: var(--txt); padding: 6px 16px; font-size: 11px; letter-spacing: 1px; cursor: pointer; clip-path: var(--clip-tag); transition: all .22s ease; }
.btn:hover { border-color: var(--yellow-dim); color: var(--yellow); }
.btn.on { background: var(--yellow); color: #000; border-color: var(--yellow); font-weight: 700; }
.spacer { flex: 1; }

.stat-table { width: 100%; border-collapse: collapse; font-size: 11px; }
.stat-table th { font-size: 10px; color: var(--txt-dim); letter-spacing: 1px; text-align: left; padding: 6px 8px; border-bottom: 1px solid var(--line); font-weight: 400; cursor: pointer; }
.stat-table th:hover { color: var(--yellow); }
.stat-table td { padding: 9px 8px; border-bottom: 1px dashed var(--line); font-family: Consolas, monospace; }
.stat-table tr { transition: background .2s ease; }
.stat-table tr:hover { background: rgba(255, 230, 0, .05); }
.stat-table .mname { display: flex; align-items: center; gap: 7px; font-family: 'Segoe UI', sans-serif; }
.stat-table .mname i { width: 8px; height: 8px; background: var(--yellow); display: inline-block; }
.stat-table .hot { color: var(--yellow); font-weight: 700; }
</style>