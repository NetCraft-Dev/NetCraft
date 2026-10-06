using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text;
using NetCraft.Config;

namespace NetCraft.Util;

//SystemReport 系统信息段 对应原版 net.minecraft.SystemReport
//崩溃报告末尾那段 System Details 就是它 硬件信息只取托管运行时可拿到的那些
//每一项都单独兜错 收集信息本身失败不能让崩溃报告写不出来
public sealed class SystemReport
{
    private const long BytesPerMebibyte = 1024 * 1024;

    private readonly List<(string Key, string Value)> _entries = new();

    public SystemReport()
    {
        SetDetail("NetCraft Version", SharedConstants.Version);
        SetDetail("Protocol Version", SharedConstants.ProtocolVersion.ToString());
        SetDetail("World Data Version", SharedConstants.WorldDataVersion.ToString());
        SetDetail("Operating System", $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
        SetDetail("Runtime", RuntimeInformation.FrameworkDescription);
        SetDetail("Runtime Identifier", RuntimeInformation.RuntimeIdentifier);
        SetDetail("Process", DescribeProcess);
        SetDetail("Memory", DescribeMemory);
        SetDetail("Memory (GC)", DescribeGcMemory);
        SetDetail("CPUs", Environment.ProcessorCount.ToString);
        SetDetail("Storage (workdir)", () => DescribeStorage(AppContext.BaseDirectory));
        SetDetail("Debug Flags", DescribeDebugFlags);
    }

    //SetDetail 直接写一条
    public void SetDetail(string key, string value) => _entries.Add((key, value));

    //SetDetail 取值失败时降级成一条错误说明 对应原版 setDetail 的 catch 分支
    public void SetDetail(string key, Func<string> valueSupplier)
    {
        try
        {
            _entries.Add((key, valueSupplier()));
        }
        catch (Exception e)
        {
            _entries.Add((key, $"~~ERROR~~ {e.GetType().Name}: {e.Message}"));
        }
    }

    //AppendToCrashReportString 按原版格式追加 System Details 段
    public void AppendToCrashReportString(StringBuilder builder)
    {
        var newLine = Environment.NewLine;
        builder.Append("-- System Details --").Append(newLine);
        builder.Append("Details:");
        foreach (var (key, value) in _entries)
            builder.Append(newLine).Append('\t').Append(key).Append(": ").Append(value);
    }

    //ToLineSeparatedString 每行一个键值对 供日志与诊断命令使用
    public string ToLineSeparatedString()
        => string.Join(Environment.NewLine, _entries.Select(e => $"{e.Key}: {e.Value}"));

    //DescribeProcess 进程运行时长 内存占用 线程数与位数
    private static string DescribeProcess()
    {
        using var process = Process.GetCurrentProcess();
        var uptime = (DateTime.Now - process.StartTime).TotalSeconds;
        return $"Uptime: {uptime:F0}s, working set: {process.WorkingSet64 / BytesPerMebibyte} MiB, "
            + $"private: {process.PrivateMemorySize64 / BytesPerMebibyte} MiB, threads: {process.Threads.Count}, "
            + $"{(Environment.Is64BitProcess ? "64" : "32")}-bit";
    }

    //DescribeMemory 托管堆已用与运行时可用上限 对应原版那条 Runtime 内存说明
    private static string DescribeMemory()
    {
        var info = GC.GetGCMemoryInfo();
        var used = GC.GetTotalMemory(false);
        return $"{used / BytesPerMebibyte} bytes({used / BytesPerMebibyte} MiB) used / "
            + $"{info.TotalAvailableMemoryBytes / BytesPerMebibyte} bytes({info.TotalAvailableMemoryBytes / BytesPerMebibyte} MiB) available";
    }

    //DescribeGcMemory 堆的提交量与碎片 以及工作站还是服务器 GC
    private static string DescribeGcMemory()
    {
        var info = GC.GetGCMemoryInfo();
        return $"heap: {info.HeapSizeBytes / BytesPerMebibyte} MiB, committed: {info.TotalCommittedBytes / BytesPerMebibyte} MiB, "
            + $"fragmented: {info.FragmentedBytes / BytesPerMebibyte} MiB, pinned objects: {info.PinnedObjectsCount}, "
            + $"GC mode: {(GCSettings.IsServerGC ? "server" : "workstation")}, latency: {GCSettings.LatencyMode}";
    }

    //DescribeStorage 程序根目录所在盘的可用与总空间 对应原版那条 workdir 空间说明
    private static string DescribeStorage(string path)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(path));
        if (string.IsNullOrEmpty(root)) return "<no such root>";
        var drive = new DriveInfo(root);
        if (!drive.IsReady) return "<drive not ready>";
        return $"available: {drive.AvailableFreeSpace / BytesPerMebibyte} MiB, total: {drive.TotalSize / BytesPerMebibyte} MiB";
    }

    //DescribeDebugFlags 当前生效的调试开关 对应原版那条 Debug Flags
    private static string DescribeDebugFlags()
    {
        var enabled = new List<string>();
        if (DebugMode.IsEnabled) enabled.Add("debug");
        return enabled.Count == 0 ? "0 total" : $"{enabled.Count} total; {string.Join(" ", enabled)}";
    }
}
