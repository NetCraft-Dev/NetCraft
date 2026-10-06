using System.Diagnostics;
using System.Text;

namespace NetCraft.Util;

//Crash report category, maps to vanilla net.minecraft.CrashReportCategory
//Holds a title, key-value details and stack
public sealed class CrashReportCategory
{
    private static readonly string NewLine = Environment.NewLine;
    private readonly string _title;
    private readonly List<Entry> _entries = new();
    private StackTrace _stackTrace = new(skipFrames: 1, fNeedFileInfo: true);

    public CrashReportCategory(string title) => _title = title;

    //Sets a detail key-value pair, maps to vanilla setDetail
    public CrashReportCategory SetDetail(string key, object? value)
    {
        _entries.Add(new Entry(key, value));
        return this;
    }

    //Fills in the current call stack, maps to vanilla fillInStackTrace
    //nestedOffset skips outer call stack frames
    public int FillInStackTrace(int nestedOffset)
    {
        _stackTrace = new StackTrace(nestedOffset + 1, fNeedFileInfo: true);
        return _stackTrace.FrameCount;
    }

    public void GetDetails(StringBuilder builder)
    {
        builder.Append("-- ").Append(_title).Append(" --").Append(NewLine);
        builder.Append("Details:");
        foreach (var entry in _entries)
        {
            builder.Append(NewLine).Append('\t').Append(entry.Key).Append(": ").Append(entry.Value);
        }
        var frames = _stackTrace.GetFrames();
        if (frames is { Length: > 0 })
        {
            builder.Append(NewLine).Append("Stacktrace:");
            foreach (var frame in frames)
            {
                builder.Append(NewLine).Append("\tat ").Append(frame);
            }
        }
    }

    public StackTrace GetStackTrace() => _stackTrace;

    //Detail entry, maps to vanilla CrashReportCategory.Entry
    private sealed class Entry
    {
        public string Key { get; }
        public string Value { get; }

        public Entry(string key, object? rawValue)
        {
            Key = key;
            Value = FormatValue(rawValue);
        }

        //Maps to vanilla Entry construction with special formatting for null and Throwable
        private static string FormatValue(object? rawValue)
        {
            if (rawValue is null) return "~~NULL~~";
            if (rawValue is Exception t) return $"~~ERROR~~ {t.GetType().Name}: {t.Message}";
            return rawValue.ToString() ?? "";
        }
    }
}
