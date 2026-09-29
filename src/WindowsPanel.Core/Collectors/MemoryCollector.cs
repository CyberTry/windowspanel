using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using WindowsPanel.Core.Models;

namespace WindowsPanel.Core.Collectors;

/// <summary>通过 GlobalMemoryStatusEx 读取物理内存使用情况（Win32 API，零开销）。</summary>
[SupportedOSPlatform("windows")]
public sealed class MemoryCollector : ICollector
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint   dwLength;
        public uint   dwMemoryLoad;
        public ulong  ullTotalPhys;
        public ulong  ullAvailPhys;
        public ulong  ullTotalPageFile;
        public ulong  ullAvailPageFile;
        public ulong  ullTotalVirtual;
        public ulong  ullAvailVirtual;
        public ulong  ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    public string Name => MetricNames.MemUsedBytes;
    public int    ConsecutiveFailures { get; private set; }
    public double UsedPercent { get; private set; }
    public long   UsedBytes   { get; private set; }
    public long   TotalBytes  { get; private set; }

    public Task SampleAsync(CancellationToken ct)
    {
        try
        {
            var ms = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (GlobalMemoryStatusEx(ref ms))
            {
                TotalBytes  = (long)ms.ullTotalPhys;
                UsedBytes   = (long)(ms.ullTotalPhys - ms.ullAvailPhys);
                UsedPercent = ms.dwMemoryLoad;
                ConsecutiveFailures = 0;
            }
            else
            {
                ConsecutiveFailures++;
            }
        }
        catch
        {
            ConsecutiveFailures++;
        }
        return Task.CompletedTask;
    }

    public void Dispose() { }
}