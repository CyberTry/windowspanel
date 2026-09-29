<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { usePanelStore } from '@/stores/panel';

const panel = usePanelStore();
const level = ref<string | undefined>(undefined);
const q = ref('');

async function reload() {
  await panel.loadEvents(level.value);
}

onMounted(reload);

const filtered = computed(() => {
  const k = q.value.toLowerCase();
  if (!k) return panel.events;
  return panel.events.filter(e =>
    e.message.toLowerCase().includes(k) ||
    (e.metric ?? '').toLowerCase().includes(k) ||
    e.category.toLowerCase().includes(k)
  );
});

const groupedByDay = computed(() => {
  const groups = new Map<string, typeof panel.events>();
  for (const e of filtered.value) {
    const d = new Date(e.ts);
    const wk = ['周日', '周一', '周二', '周三', '周四', '周五', '周六'][d.getDay()];
    const k = `${String(d.getMonth() + 1).padStart(2, '0')}·${String(d.getDate()).padStart(2, '0')} ${d.getFullYear()} · ${wk}`;
    if (!groups.has(k)) groups.set(k, []);
    groups.get(k)!.push(e);
  }
  return [...groups.entries()];
});

function fmtTime(ts: number) {
  const d = new Date(ts);
  return `${String(d.getFullYear())}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')} ${String(d.getHours()).padStart(2,'0')}:${String(d.getMinutes()).padStart(2,'0')}:${String(d.getSeconds()).padStart(2,'0')}`;
}

const stats = computed(() => ({
  info:  panel.events.filter(e => e.level === 'info').length,
  warn:  panel.events.filter(e => e.level === 'warn').length,
  error: panel.events.filter(e => e.level === 'error').length
}));
</script>

<template>
  <section class="page">
    <div class="log-filters">
      <button class="btn" :class="{ on: level === undefined }" @click="level = undefined; reload()">全部</button>
      <button class="btn" :class="{ on: level === 'info' }"  @click="level = 'info'; reload()">INFO</button>
      <button class="btn warn-on" :class="{ on: level === 'warn' }" @click="level = 'warn'; reload()">WARN</button>
      <button class="btn" :class="{ on: level === 'error' }" @click="level = 'error'; reload()">ERROR</button>
      <div class="search"><span>⌕</span><input v-model="q" placeholder="搜索关键字…" /></div>
      <div class="spacer" />
      <button class="btn">导出</button>
      <button class="btn" @click="reload()">刷新</button>
    </div>

    <div class="logs-grid">
      <div class="panel">
        <div class="p-head">
          <span class="tri"></span><h2>事件日志</h2>
          <span class="en">EVENT LOG</span>
          <div class="tools">
            <span class="badge warn">{{ filtered.length }} / {{ panel.events.length }} ENTRIES</span>
          </div>
        </div>
        <div class="p-body" style="padding:0 14px 10px">
          <template v-for="[day, items] in groupedByDay" :key="day">
            <div class="day-head">
              <b>{{ day.split(' ')[0] }}</b>
              <span>{{ day }}</span>
              <span class="cnt">{{ items.length }} ENTRIES</span>
            </div>
            <div v-for="e in items" :key="e.id" class="log-row">
              <span class="lvl" :class="e.level">{{ e.level.toUpperCase() }}</span>
              <span class="log-time">{{ fmtTime(e.ts) }}</span>
              <span class="log-msg" v-html="e.message"></span>
              <span class="log-cat">{{ e.category }}</span>
            </div>
          </template>
          <div v-if="filtered.length === 0" class="log-empty-day">暂无日志记录</div>
        </div>
        <div class="corner" />
      </div>

      <div>
        <div class="panel">
          <div class="p-head"><span class="tri"></span><h2>级别统计</h2><span class="en">BY LEVEL</span></div>
          <div class="p-body">
            <div class="side-stat"><span class="lvl error" style="width:52px">ERROR</span><span class="v">{{ stats.error }}</span></div>
            <div class="side-stat"><span class="lvl warn"  style="width:52px">WARN</span><span class="v">{{ stats.warn }}</span></div>
            <div class="side-stat"><span class="lvl info"  style="width:52px">INFO</span><span class="v">{{ stats.info }}</span></div>
          </div>
        </div>

        <div class="panel">
          <div class="p-head"><span class="tri"></span><h2>告警规则</h2><span class="en">THRESHOLDS</span></div>
          <div class="p-body">
            <table class="th-table">
              <tr><td>CPU 持续高载</td><td><span class="y">&gt; 90% · 5 min</span></td></tr>
              <tr><td>内存压力</td><td>可用 &lt; 10%</td></tr>
              <tr><td>显存压力</td><td>&gt; 95%</td></tr>
              <tr><td>采集器降级</td><td>连续失败 3 次</td></tr>
            </table>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>

<style scoped>
.page { padding: 14px 16px; height: 100%; overflow-y: auto; }
.log-filters { display: flex; gap: 6px; align-items: center; margin-bottom: 12px; flex-wrap: wrap; }
.btn { border: 1px solid var(--line); background: var(--panel); color: var(--txt); padding: 6px 16px; font-size: 11px; letter-spacing: 1px; cursor: pointer; clip-path: var(--clip-tag); transition: all .22s ease; }
.btn:hover { border-color: var(--yellow-dim); color: var(--yellow); }
.btn.on { background: var(--yellow); color: #000; border-color: var(--yellow); font-weight: 700; }
.btn.warn-on { background: var(--orange); color: #000; border-color: var(--orange); font-weight: 700; }
.search { display: flex; align-items: center; gap: 6px; border: 1px solid var(--line); background: var(--panel); padding: 5px 10px; clip-path: var(--clip-tag); margin-left: auto; }
.search input { background: none; border: none; outline: none; color: var(--txt); font-size: 11px; width: 180px; }
.spacer { flex: 1; }

.logs-grid { display: grid; grid-template-columns: 1fr 280px; gap: 12px; }

.day-head { display: flex; align-items: baseline; gap: 10px; padding: 10px 2px 6px; color: var(--txt-dim); font-size: 11px; letter-spacing: 1px; border-bottom: 1px solid var(--line); }
.day-head b { color: var(--yellow); }
.day-head .cnt { margin-left: auto; font-size: 10px; }

.log-row { display: grid; grid-template-columns: 64px 170px 1fr 70px; gap: 10px; align-items: center; padding: 9px 8px; border-bottom: 1px dashed var(--line); font-size: 11px; position: relative; transition: background .2s ease, transform .22s cubic-bezier(.2, .7, .2, 1); }
.log-row:hover { background: rgba(255, 230, 0, .04); transform: translateX(5px); }
.log-row:hover::before { content: ""; position: absolute; left: 0; top: 0; bottom: 0; width: 2px; background: var(--yellow); }

.lvl { font-size: 9px; text-align: center; padding: 3px 0; letter-spacing: 1px; clip-path: var(--clip-tag); font-weight: 700; }
.lvl.info  { background: rgba(62, 198, 255, .12); color: var(--cyan);   border: 1px solid rgba(62, 198, 255, .4); }
.lvl.warn  { background: rgba(255, 230, 0, .12); color: var(--yellow); border: 1px solid rgba(255, 230, 0, .4); }
.lvl.error { background: rgba(255, 59, 59, .12);  color: var(--red);    border: 1px solid rgba(255, 59, 59, .4); }

.log-time { color: var(--txt-dim); font-family: Consolas, monospace; font-size: 10px; }
.log-msg b { color: var(--yellow); }
.log-cat { color: var(--txt-dim); font-size: 10px; text-align: right; }
.log-empty-day { color: var(--txt-dim); font-size: 10px; padding: 16px; letter-spacing: 2px; text-align: center; }

.side-stat { display: flex; justify-content: space-between; align-items: center; padding: 8px 4px; border-bottom: 1px dashed var(--line); font-size: 11px; }
.side-stat:last-child { border-bottom: none; }
.side-stat .v { font-family: Consolas, monospace; }

.th-table { width: 100%; font-size: 11px; border-collapse: collapse; }
.th-table td { padding: 7px 4px; border-bottom: 1px dashed var(--line); }
.th-table td:first-child { color: var(--txt-dim); }
.th-table td:last-child { text-align: right; font-family: Consolas, monospace; }
.th-table td:last-child .y { color: var(--yellow); }
</style>