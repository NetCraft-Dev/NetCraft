using NetCraft.Logging;

namespace NetCraft.Util;

//CrashHandler 全局托管异常兜底 对应原版客户端与服务端共用的那一套崩溃处理
//收两类异常 进程级未捕获异常与没被观察的任务异常
//报告写到程序根目录 crash-reports/ 下 文件名带时间与角色 客户端与服务端只是角色名不同
public static class CrashHandler
{
    private static readonly object Sync = new();
    private static string _role = "netcraft";
    private static string _rootDirectory = AppContext.BaseDirectory;
    private static ReportType _reportType = ReportType.Crash;
    private static bool _installed;
    private static bool _unobservedSaved;

    //Role 报告文件名里的角色段 默认 netcraft
    //客户端与服务端启动时分别设成 client 与 server 就能对齐原版命名
    //写报告时才读 所以什么时候设都生效 不要求在安装之前
    public static string Role
    {
        get { lock (Sync) return _role; }
        set { lock (Sync) _role = string.IsNullOrWhiteSpace(value) ? "netcraft" : value; }
    }

    //Installed 是否已装上处理器
    public static bool Installed { get { lock (Sync) return _installed; } }

    //CrashReportsDir 报告目录 落在程序根目录的 crash-reports/ 下
    public static string CrashReportsDir { get { lock (Sync) return Path.Combine(_rootDirectory, "crash-reports"); } }

    //Install 装上全局异常处理器 幂等
    //rootDirectory 程序根目录 为空取 AppContext.BaseDirectory
    //reportType 默认报告类型 单次崩溃可以覆盖
    public static void Install(string? rootDirectory = null, ReportType? reportType = null)
    {
        lock (Sync)
        {
            if (_installed) return;
            if (!string.IsNullOrWhiteSpace(rootDirectory)) _rootDirectory = rootDirectory;
            _reportType = reportType ?? ReportType.Crash;
            _installed = true;
        }
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    //CrashReportFilename 报告文件名 对应原版 crash-<时间>-<角色>.txt
    public static string CrashReportFilename(string? role = null)
        => $"crash-{DateTime.Now:yyyy-MM-dd_HH.mm.ss}-{role ?? Role}.txt";

    //Handle 处理一次崩溃 构造报告并落盘 返回报告路径 写不出来返回 null
    public static string? Handle(Exception exception, string title, IReadOnlyList<string>? extraComments = null,
        ReportType? reportType = null)
        => Save(CrashReport.ForThrowable(exception, title), extraComments, reportType);

    //Save 把报告写进 crash-reports/ 并在日志里报出路径
    //写不出来时把整份报告打进日志 现场不能因为磁盘问题就丢
    public static string? Save(CrashReport report, IReadOnlyList<string>? extraComments = null,
        ReportType? reportType = null)
    {
        var type = reportType ?? _reportType;
        var path = Path.Combine(CrashReportsDir, CrashReportFilename());
        if (!report.SaveToFile(path, type, extraComments))
        {
            Log.Critical($"崩溃报告写入失败 报告内容如下{Environment.NewLine}{report.GetFriendlyReport(type, extraComments)}");
            return null;
        }
        Log.Critical($"崩溃报告已写入 {path}");
        return path;
    }

    //OnUnhandledException 进程级未捕获异常
    //这一路进程即将终止 报告一定要写出去 写完把日志刷干净再交还控制权
    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is not Exception exception) return;
        var path = Handle(exception, "未捕获的异常", new[] { $"Fatal: {e.IsTerminating}" });
        Log.Exception(exception, path is null ? "未捕获的异常" : $"未捕获的异常 报告 {path}");
        Log.Flush();
    }

    //OnUnobservedTaskException 没被等待的任务异常
    //这一路默认不终止进程 每个都写报告会被刷爆 所以只留第一份 之后只记日志
    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        bool first;
        lock (Sync)
        {
            first = !_unobservedSaved;
            _unobservedSaved = true;
        }
        if (!first)
        {
            Log.Exception(e.Exception, "又一处未观察的任务异常 报告只留第一份");
            return;
        }
        var path = Handle(e.Exception, "未观察到的任务异常");
        Log.Exception(e.Exception, path is null ? "未观察到的任务异常" : $"未观察到的任务异常 报告 {path}");
    }
}
