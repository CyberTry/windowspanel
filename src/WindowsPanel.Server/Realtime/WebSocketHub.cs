using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace WindowsPanel.Server.Realtime;

/// <summary>
/// 全局 WebSocket 客户端注册表。CollectorWorker 每 1s 调用 BroadcastAsync 推送 LiveSample。
/// 消息格式：{ "type": "live", "data": { ...LiveSample... } }。
/// </summary>
public sealed class WebSocketHub
{
    private readonly ConcurrentDictionary<Guid, WebSocket> clients = new();
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public int ClientCount => clients.Count;

    public void Attach(WebApplication app)
    {
        app.Map("/ws", async (HttpContext ctx) =>
        {
            if (!ctx.WebSockets.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync("expected websocket");
                return;
            }
            using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
            var id = Guid.NewGuid();
            clients[id] = ws;
            try
            {
                var buf = new byte[4096];
                while (ws.State == WebSocketState.Open)
                {
                    var r = await ws.ReceiveAsync(buf, ctx.RequestAborted);
                    if (r.MessageType == WebSocketMessageType.Close) break;
                }
            }
            catch { /* 客户端异常断开 */ }
            finally { clients.TryRemove(id, out _); }
        });
    }

    public async Task BroadcastAsync<T>(string type, T payload)
    {
        if (clients.IsEmpty) return;

        var envelope = new { type, data = payload };
        var json = JsonSerializer.Serialize(envelope, JsonOpts);
        var bytes = Encoding.UTF8.GetBytes(json);

        var snapshot = clients.ToArray();
        foreach (var (id, ws) in snapshot)
        {
            if (ws.State != WebSocketState.Open) { clients.TryRemove(id, out _); continue; }
            try
            {
                await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch
            {
                clients.TryRemove(id, out _);
            }
        }
    }
}