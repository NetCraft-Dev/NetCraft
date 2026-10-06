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
    private StackTrace _uncategorizedStackTrace = new(skipFrames: 1, fNeedFileInfo: true);

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
            category.FillInStackTrace(nestedOffset + 1);
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
        if (_uncategorizedStackTrace.FrameCount > 0)
        {
            builder.Append("-- Head --").Append(NewLine);
            builder.Append("Thread: ").Append(Environment.CurrentManagedThreadId).Append(NewLine);
            builder.Append("Stacktrace:").Append(NewLine);
            foreach (var frame in _uncategorizedStackTrace.GetFrames() ?? Array.Empty<StackFrame>())
            {
                builder.Append("\tat ").Append(frame).Append(NewLine);
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

    //Prints the exception message and stack
    private string GetExceptionMessage()
    {
        var ex = _exception;
        return ex.ToString();
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
}
