using System.Collections.Concurrent;
using WindowsPanel.Core.Models;

namespace WindowsPanel.Core.Aggregation;

/// <summary>
/// 线程安全滑动窗口：保留最近 N 帧 LiveSample，供前端 5 分钟实时曲线查询。
/// 默认 300 帧 = 5 分钟 @ 1Hz。
/// </summary>
public sealed class RingBuffer
{
    private readonly ConcurrentQueue<LiveSample> queue = new();
    private readonly int capacity;

    public int Capacity => capacity;

    public RingBuffer(int capacity = 300) => this.capacity = capacity;

    public void Push(LiveSample s)
    {
        queue.Enqueue(s);
        while (queue.Count > capacity) queue.TryDequeue(out _);
    }

    public IReadOnlyList<LiveSample> Snapshot()
    {
        var arr = new LiveSample[queue.Count];
        queue.CopyTo(arr, 0);
        return arr.Length > capacity ? arr[^capacity..] : arr;
    }

    public void Clear() => queue.Clear();
}