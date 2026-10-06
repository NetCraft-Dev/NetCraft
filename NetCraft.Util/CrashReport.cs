using System.Diagnostics;
using System.Text;
using NetCraft.Logging;

namespace NetCraft.Util;

//崩溃报告对应原版net.minecraft.CrashReport
//收集异常与上下文分类用于错误诊断 末尾附一段 System Details 系统信息
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

    //SaveFile 报告落盘的路径 没写过是 null 对应原版 getSaveFile
    public string? SaveFile => _saveFile;

    //SystemReport 系统信息段 对应原版 getSystemReport
    public SystemReport SystemReport => _systemReport;

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

    //GetFriendlyReport 不带报告头的精简版本 供日志与诊断命令直接取用
    public string GetFriendlyReport() => GetFriendlyReport(ReportType.Crash);

    //GetFriendlyReport 完整报告文本 对应原版 getFriendlyReport
    //头部两行来自报告类型 之后是时间 描述 异常堆栈与各分类详情
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

    //SaveToFile 把报告写到指定路径 已写过直接返回 false 对应原版 saveToFile
    //写失败只记日志不抛 崩溃处理链路上再抛就没有兜底了
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
            Log.Error($"崩溃报告写入失败 {path}: {e.GetType().Name}: {e.Message}");
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
