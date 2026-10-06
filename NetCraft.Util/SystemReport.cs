using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text;
using NetCraft.Config;

namespace NetCraft.Util;

//SystemReport the System Details section, maps to vanilla net.minecraft.SystemReport
//It is the System Details section at the end of a crash report; hardware info only covers what the managed runtime exposes
//Each item is individually error-guarded; a failure collecting info must not prevent the crash report from being written
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

    //SetDetail writes one entry directly
    public void SetDetail(string key, string value) => _entries.Add((key, value));

    //SetDetail degrades to an error note when value retrieval fails, maps to the catch branch of vanilla setDetail
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

    //AppendToCrashReportString appends the System Details section in vanilla format
    public void AppendToCrashReportString(StringBuilder builder)
    {
        var newLine = Environment.NewLine;
        builder.Append("-- System Details --").Append(newLine);
        builder.Append("Details:");
        foreach (var (key, value) in _entries)
            builder.Append(newLine).Append('\t').Append(key).Append(": ").Append(value);
    }

    //ToLineSeparatedString one key-value pair per line, for logs and diagnostic commands
    public string ToLineSeparatedString()
        => string.Join(Environment.NewLine, _entries.Select(e => $"{e.Key}: {e.Value}"));

    //DescribeProcess process uptime, memory usage, thread count and bitness
    private static string DescribeProcess()
    {
        using var process = Process.GetCurrentProcess();
        var uptime = (DateTime.Now - process.StartTime).TotalSeconds;
        return $"Uptime: {uptime:F0}s, working set: {process.WorkingSet64 / BytesPerMebibyte} MiB, "
            + $"private: {process.PrivateMemorySize64 / BytesPerMebibyte} MiB, threads: {process.Threads.Count}, "
            + $"{(Environment.Is64BitProcess ? "64" : "32")}-bit";
    }

    //DescribeMemory managed heap used and runtime available limit, maps to the vanilla Runtime memory note
    private static string DescribeMemory()
    {
        var info = GC.GetGCMemoryInfo();
        var used = GC.GetTotalMemory(false);
        return $"{used} bytes({used / BytesPerMebibyte} MiB) used / "
            + $"{info.TotalAvailableMemoryBytes} bytes({info.TotalAvailableMemoryBytes / BytesPerMebibyte} MiB) available";
    }

    //DescribeGcMemory heap committed and fragmentation, and whether GC is workstation or server
    private static string DescribeGcMemory()
    {
        var info = GC.GetGCMemoryInfo();
        return $"heap: {info.HeapSizeBytes / BytesPerMebibyte} MiB, committed: {info.TotalCommittedBytes / BytesPerMebibyte} MiB, "
            + $"fragmented: {info.FragmentedBytes / BytesPerMebibyte} MiB, pinned objects: {info.PinnedObjectsCount}, "
            + $"GC mode: {(GCSettings.IsServerGC ? "server" : "workstation")}, latency: {GCSettings.LatencyMode}";
    }

    //DescribeStorage free and total space of the disk holding the program root, maps to the vanilla workdir space note
    private static string DescribeStorage(string path)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(path));
        if (string.IsNullOrEmpty(root)) return "<no such root>";
        var drive = new DriveInfo(root);
        if (!drive.IsReady) return "<drive not ready>";
        return $"available: {drive.AvailableFreeSpace / BytesPerMebibyte} MiB, total: {drive.TotalSize / BytesPerMebibyte} MiB";
    }

    //DescribeDebugFlags the currently effective debug flags, maps to the vanilla Debug Flags note
    private static string DescribeDebugFlags()
    {
        var enabled = new List<string>();
        if (DebugMode.IsEnabled) enabled.Add("debug");
        return enabled.Count == 0 ? "0 total" : $"{enabled.Count} total; {string.Join(" ", enabled)}";
    }
}
