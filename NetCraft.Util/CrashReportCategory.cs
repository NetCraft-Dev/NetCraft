using System.Diagnostics;
using System.Text;

namespace NetCraft.Util;

//Crash report category, maps to vanilla net.minecraft.CrashReportCategory
//Holds a title, key-value details and the frames that belong to this part of the report
public sealed class CrashReportCategory
{
    private static readonly string NewLine = Environment.NewLine;
    private readonly string _title;
    private readonly List<Entry> _entries = new();
    private StackFrame[] _frames = Array.Empty<StackFrame>();

    public CrashReportCategory(string title) => _title = title;

    //Sets a detail key-value pair, maps to vanilla setDetail
    public CrashReportCategory SetDetail(string key, object? value)
    {
        _entries.Add(new Entry(key, value));
        return this;
    }

    //SetDetail evaluates the value lazily and degrades to an error note when it throws, maps to vanilla setDetail with a callback
    //Vanilla uses this form wherever reading the detail could itself fail; one bad detail must not cost the whole report
    public CrashReportCategory SetDetail(string key, Func<string> valueSupplier)
    {
        try
        {
            _entries.Add(new Entry(key, valueSupplier()));
        }
        catch (Exception e)
        {
            _entries.Add(new Entry(key, e));
        }
        return this;
    }

    //FillInStackTrace captures the current call stack and returns its frame count, maps to vanilla fillInStackTrace
    //nestedOffset skips the outer call frames so the first captured frame is the one that reported the failure
    public int FillInStackTrace(int nestedOffset)
    {
        _frames = new StackTrace(nestedOffset + 1, fNeedFileInfo: true).GetFrames() ?? Array.Empty<StackFrame>();
        return _frames.Length;
    }

    //ValidateStackTrace checks this category's own stack against the exception's stack, maps to vanilla validateStackTrace
    //Vanilla uses the verdict to decide whether splitting the exception trace any further is still meaningful; the first
    //frame is replaced by the exception's own frame so the report prints the position the exception recorded
    public bool ValidateStackTrace(StackFrame? source, StackFrame? next)
    {
        if (_frames.Length == 0 || source is null) return false;
        if (!SameFrame(_frames[0], source)) return false;
        if ((next is not null) != (_frames.Length > 1)) return false;
        if (next is not null && !SameFrame(_frames[1], next)) return false;
        _frames[0] = source;
        return true;
    }

    //SameFrame compares two frames the way vanilla compares stack trace elements
    //The CLR gives StackFrame no value equality, so declaring type, method name and file are compared by hand
    private static bool SameFrame(StackFrame left, StackFrame right)
    {
        var leftMethod = left.GetMethod();
        var rightMethod = right.GetMethod();
        if (leftMethod is null || rightMethod is null) return leftMethod is null && rightMethod is null;
        return leftMethod.Name == rightMethod.Name
            && leftMethod.DeclaringType?.FullName == rightMethod.DeclaringType?.FullName
            && left.GetFileName() == right.GetFileName();
    }

    public void GetDetails(StringBuilder builder)
    {
        builder.Append("-- ").Append(_title).Append(" --").Append(NewLine);
        builder.Append("Details:");
        foreach (var entry in _entries)
        {
            builder.Append(NewLine).Append('\t').Append(entry.Key).Append(": ").Append(entry.Value);
        }
        if (_frames.Length > 0)
        {
            builder.Append(NewLine).Append("Stacktrace:");
            foreach (var frame in _frames)
            {
                builder.Append(NewLine).Append("\tat ").Append(CrashReport.FormatFrame(frame));
            }
        }
    }

    public StackFrame[] GetStackTrace() => _frames;

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

        //Maps to vanilla Entry construction with special formatting for null and exceptions
        private static string FormatValue(object? rawValue)
        {
            if (rawValue is null) return "~~NULL~~";
            if (rawValue is Exception t) return $"~~ERROR~~ {t.GetType().Name}: {t.Message}";
            return rawValue.ToString() ?? "";
        }
    }
}
