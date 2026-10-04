using System.Diagnostics;
using System.Text;

namespace NetCraft.Util;

//崩溃报告对应原版net.minecraft.CrashReport
//收集异常与上下文分类用于错误诊断
public sealed class CrashReport
{
    private static readonly string NewLine = Environment.NewLine;
    private readonly string _title;
    private readonly Exception _exception;
    private readonly List<CrashReportCategory> _details = new();
    private bool _trackingStackTrace = true;
    private StackTrace _uncategorizedStackTrace = new(skipFrames: 1, fNeedFileInfo: true);

    public CrashReport(string title, Exception exception)
    {
        _title = title;
        _exception = exception;
    }

    public string Title => _title;
    public Exception Exception => _exception;

    //添加上下文分类对应原版addCategory
    //nestedOffset用于跳过当前栈帧
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

    public string GetFriendlyReport()
    {
        var builder = new StringBuilder();
        builder.Append("Time: ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append(NewLine);
        builder.Append("Description: ").Append(_title).Append(NewLine).Append(NewLine);
        builder.Append(GetExceptionMessage());
        builder.Append(NewLine).Append(NewLine);
        builder.Append("A detailed walkthrough of the error, its code path and all known details is as follows:").Append(NewLine);
        builder.Append(new string('-', 87)).Append(NewLine).Append(NewLine);
        GetDetails(builder);
        return builder.ToString();
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
    }

    //输出异常message与堆栈
    private string GetExceptionMessage()
    {
        var ex = _exception;
        return ex.ToString();
    }

    //从Throwable构造CrashReport对应原版forThrowable
    //解包AggregateException并复用ReportedException内嵌的report
    public static CrashReport ForThrowable(Exception throwable, string title)
    {
        while (throwable is AggregateException agg && agg.InnerException != null)
            throwable = agg.InnerException;
        if (throwable is ReportedException reported)
            return reported.Report;
        return new CrashReport(title, throwable);
    }
}
