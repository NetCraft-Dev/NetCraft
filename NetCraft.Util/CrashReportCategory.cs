using System.Diagnostics;
using System.Text;

namespace NetCraft.Util;

//崩溃报告分类对应原版net.minecraft.CrashReportCategory
//包含标题键值对详情与堆栈
public sealed class CrashReportCategory
{
    private static readonly string NewLine = Environment.NewLine;
    private readonly string _title;
    private readonly List<Entry> _entries = new();
    private StackTrace _stackTrace = new(skipFrames: 1, fNeedFileInfo: true);

    public CrashReportCategory(string title) => _title = title;

    //设置详情键值对对应原版setDetail
    public CrashReportCategory SetDetail(string key, object? value)
    {
        _entries.Add(new Entry(key, value));
        return this;
    }

    //填充当前调用栈对应原版fillInStackTrace
    //nestedOffset跳过外层调用栈帧
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

    //详情条目对应原版CrashReportCategory.Entry
    private sealed class Entry
    {
        public string Key { get; }
        public string Value { get; }

        public Entry(string key, object? rawValue)
        {
            Key = key;
            Value = FormatValue(rawValue);
        }

        //对应原版Entry构造对null与Throwable的特殊格式化
        private static string FormatValue(object? rawValue)
        {
            if (rawValue is null) return "~~NULL~~";
            if (rawValue is Exception t) return $"~~ERROR~~ {t.GetType().Name}: {t.Message}";
            return rawValue.ToString() ?? "";
        }
    }
}
