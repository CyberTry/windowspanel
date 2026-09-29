using WindowsPanel.Core.Models;

namespace WindowsPanel.Server.Hosting;

/// <summary>单例全局状态：最新一份 PanelSnapshot，供 API + 缓存页面读取。</summary>
public sealed class PanelState
{
    private readonly object lockObj = new();
    public PanelSnapshot? Current { get; private set; }

    public void Update(PanelSnapshot snap) { lock (lockObj) Current = snap; }
    public PanelSnapshot? Get()           { lock (lockObj) return Current; }
}