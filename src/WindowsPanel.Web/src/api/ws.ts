// LiveSocket — 自动重连的 WS 客户端，订阅 CollectorWorker 每 1s 推送的 LiveSample。
// 消息格式（来自后端 WebSocketHub.BroadcastAsync）：{ "type": "live", "data": { ...LiveSample... } }

type Handler = (msg: { type: string; data: any }) => void;

export class LiveSocket {
  private ws?: WebSocket;
  private handlers = new Set<Handler>();
  private retryDelay = 1000;
  private status: 'connecting' | 'open' | 'closed' = 'closed';

  start() {
    if (this.ws || this.status === 'connecting') return;
    const proto = location.protocol === 'https:' ? 'wss:' : 'ws:';
    const url = `${proto}//${location.host}/ws`;
    this.status = 'connecting';
    const ws = new WebSocket(url);
    this.ws = ws;
    ws.onmessage = (e) => {
      try { const msg = JSON.parse(e.data); this.handlers.forEach(h => h(msg)); } catch { /* ignore */ }
    };
    ws.onopen = () => { this.status = 'open'; this.retryDelay = 1000; };
    ws.onclose = () => {
      this.ws = undefined;
      this.status = 'closed';
      setTimeout(() => this.start(), this.retryDelay);
      this.retryDelay = Math.min(this.retryDelay * 1.5, 10000);
    };
    ws.onerror = () => { ws.close(); };
  }

  stop() {
    this.ws?.close();
    this.ws = undefined;
    this.status = 'closed';
  }

  onMsg(h: Handler) { this.handlers.add(h); return () => { this.handlers.delete(h); }; }

  getStatus() { return this.status; }
}

export const liveSocket = new LiveSocket();