using System.Diagnostics;
using System.Text;
using NetCraft.Logging;

namespace NetCraft.Util;

//Crash report, maps to vanilla net.minecraft.CrashReport
//Collects the exception and context categories for diagnostics; appends a System Details section at the end
public sealed class CrashReport
{
    private static readonly string NewLine = Environment.NewLine;
    private readonly string _title;
    private readonly Exception _exception;
    private readonly List<CrashReportCategory> _details = new();
    private readonly SystemReport _systemReport = new();
    private string? _saveFile;
    private bool _trackingStackTrace = true;
    private StackFrame[] _uncategorizedStackTrace = Array.Empty<StackFrame>();

    public CrashReport(string title, Exception exception)
    {
        _title = title;
        _exception = exception;
    }

    public string Title => _title;
    public Exception Exception => _exception;

    //SaveFile path the report was written to, null if never written, maps to vanilla getSaveFile
    public string? SaveFile => _saveFile;

    //SystemReport the System Details section, maps to vanilla getSystemReport
    public SystemReport SystemReport => _systemReport;

    //Adds a context category, maps to vanilla addCategory
    //nestedOffset is used to skip the current stack frame
    public CrashReportCategory AddCategory(string name) => AddCategory(name, 1);

    public CrashReportCategory AddCategory(string name, int nestedOffset)
    {
        var category = new CrashReportCategory(name);
        if (_trackingStackTrace)
        {
            //Vanilla compares how deep this category sits against how deep the exception was thrown; the difference is
            //the part of the exception trace nothing has claimed yet, and that part becomes the Head section
            var size = category.FillInStackTrace(nestedOffset + 1);
            var fullTrace = new StackTrace(_exception, fNeedFileInfo: true).GetFrames() ?? Array.Empty<StackFrame>();
            var traceIndex = fullTrace.Length - size;
            var source = traceIndex >= 0 && traceIndex < fullTrace.Length ? fullTrace[traceIndex] : null;
            var next = traceIndex + 1 >= 0 && traceIndex + 1 < fullTrace.Length ? fullTrace[traceIndex + 1] : null;
            _trackingStackTrace = category.ValidateStackTrace(source, next);
            if (fullTrace.Length >= size && traceIndex >= 0 && traceIndex < fullTrace.Length)
                _uncategorizedStackTrace = fullTrace[..traceIndex];
            else
                _trackingStackTrace = false;
        }
        _details.Add(category);
        return category;
    }

    //GetFriendlyReport condensed version without the report header, for direct use by logs and diagnostic commands
    public string GetFriendlyReport() => GetFriendlyReport(ReportType.Crash);

    //GetFriendlyReport full report text, maps to vanilla getFriendlyReport
    //The first two lines come from the report type, followed by time, description, exception stack and category details
    public string GetFriendlyReport(ReportType reportType, IReadOnlyList<string>? extraComments = null)
    {
        var builder = new StringBuilder();
        reportType.AppendHeader(builder, extraComments ?? Array.Empty<string>());
        builder.Append("Time: ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append(NewLine);
        builder.Append("Description: ").Append(_title).Append(NewLine).Append(NewLine);
        builder.Append(GetExceptionMessage());
        builder.Append(NewLine).Append(NewLine);
        builder.Append("A detailed walkthrough of the error, its code path and all known details is as follows:").Append(NewLine);
        builder.Append(new string('-', 87)).Append(NewLine).Append(NewLine);
        GetDetails(builder);
        return builder.ToString();
    }

    //SaveToFile writes the report to the given path, returns false if already written, maps to vanilla saveToFile
    //On write failure, only log and do not throw; throwing again on the crash path leaves nothing to fall back on
    public bool SaveToFile(string path, ReportType reportType, IReadOnlyList<string>? extraComments = null)
    {
        if (_saveFile is not null) return false;
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, GetFriendlyReport(reportType, extraComments), Encoding.UTF8);
            _saveFile = path;
            return true;
        }
        catch (Exception e)
        {
            Log.Error($"Failed to write crash report {path}: {e.GetType().Name}: {e.Message}");
            return false;
        }
    }

    public void GetDetails(StringBuilder builder)
    {
        if (_uncategorizedStackTrace.Length > 0)
        {
            builder.Append("-- Head --").Append(NewLine);
            builder.Append("Thread: ").Append(System.Threading.Thread.CurrentThread.Name ?? $"#{Environment.CurrentManagedThreadId}").Append(NewLine);
            builder.Append("Stacktrace:").Append(NewLine);
            foreach (var frame in _uncategorizedStackTrace)
            {
                builder.Append("\tat ").Append(FormatFrame(frame)).Append(NewLine);
            }
            builder.Append(NewLine);
        }
        foreach (var category in _details)
        {
            category.GetDetails(builder);
            builder.Append(NewLine).Append(NewLine);
        }
        _systemReport.AppendToCrashReportString(builder);
    }

    //GetExceptionMessage prints the exception type, message and stack
    //Vanilla builds a fresh NullPointerException / StackOverflowError / OutOfMemoryError carrying the report
    //title when the original has no message; the CLR cannot rebuild an exception's inner exception and stack,
    //so the title is written in place of the missing message instead
    private string GetExceptionMessage()
    {
        var builder = new StringBuilder();
        AppendException(builder, _exception, isCause: false);
        return builder.ToString();
    }

    //AppendException writes one exception and its causes in the vanilla stack trace layout
    //Vanilla prints a frame as "\tat type.method(file:line)" and marks nested exceptions with "Caused by:"
    private void AppendException(StringBuilder builder, Exception ex, bool isCause)
    {
        var message = string.IsNullOrEmpty(ex.Message) && NeedsTitleFallback(ex) ? _title : ex.Message;
        if (isCause) builder.Append("Caused by: ");
        builder.Append(ex.GetType().FullName).Append(": ").Append(message).Append(NewLine);
        foreach (var frame in new StackTrace(ex, fNeedFileInfo: true).GetFrames() ?? Array.Empty<StackFrame>())
            builder.Append("\tat ").Append(FormatFrame(frame)).Append(NewLine);
        if (ex.InnerException is not null) AppendException(builder, ex.InnerException, isCause: true);
    }

    //NeedsTitleFallback the exception kinds vanilla replaces with a titled copy
    private static bool NeedsTitleFallback(Exception ex)
        => ex is NullReferenceException or StackOverflowException or OutOfMemoryException;

    //FormatFrame renders one frame the way vanilla prints a stack trace element
    //The CLR's own StackFrame.ToString carries an IL offset, a trailing line break and column info; vanilla shows none of those
    internal static string FormatFrame(StackFrame frame)
    {
        var method = frame.GetMethod();
        var type = method?.DeclaringType?.FullName ?? "<unknown>";
        var name = method?.Name ?? "<unknown>";
        var file = frame.GetFileName();
        return file is null
            ? $"{type}.{name}(Unknown Source)"
            : $"{type}.{name}({file}:{frame.GetFileLineNumber()})";
    }

    //Builds a CrashReport from a Throwable, maps to vanilla forThrowable
    //Unwraps AggregateException and reuses the report embedded in ReportedException
    public static CrashReport ForThrowable(Exception throwable, string title)
    {
        while (throwable is AggregateException agg && agg.InnerException != null)
            throwable = agg.InnerException;
        if (throwable is ReportedException reported)
            return reported.Report;
        return new CrashReport(title, throwable);
    }

    //Preload reserves the memory block and builds one report so the crash path is already warm, maps to vanilla preload
    //Vanilla runs it at startup: the report code is jitted and every type it touches is loaded, so a report still gets
    //written when the process is already in a bad state
    public static void Preload()
    {
        MemoryReserve.Allocate();
        new CrashReport("Don't panic!", new Exception()).GetFriendlyReport(ReportType.Crash);
    }
}
