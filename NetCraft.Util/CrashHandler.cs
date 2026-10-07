using NetCraft.Logging;

namespace NetCraft.Util;

//CrashHandler global managed exception fallback, maps to the crash handling shared by the vanilla client and server
//Handles two kinds of exceptions: process-level uncaught and unobserved task exceptions
//Reports are written to crash-reports/ under the program root, the filename carries time and role; client and server differ only by role name
public static class CrashHandler
{
    private static readonly object Sync = new();
    private static string _role = "netcraft";
    private static Func<string> _rootProvider = () => AppContext.BaseDirectory;
    private static ReportType _reportType = ReportType.Crash;
    private static bool _installed;
    private static bool _unobservedSaved;

    //Role the role segment in the report filename, defaults to netcraft
    //Set to client and server at client/server startup respectively to align with vanilla naming
    //Read only when writing a report, so setting it any time takes effect; not required before install
    public static string Role
    {
        get { lock (Sync) return _role; }
        set { lock (Sync) _role = string.IsNullOrWhiteSpace(value) ? "netcraft" : value; }
    }

    //Installed whether the handler has been installed
    public static bool Installed { get { lock (Sync) return _installed; } }

    //CrashReportsDir report directory, located at crash-reports/ under the program root
    public static string CrashReportsDir { get { lock (Sync) return Path.Combine(_rootProvider(), "crash-reports"); } }

    //SetRootProvider injects the program root provider, installed by the main library at module init
    //Takes a delegate instead of a path: the program root may be changed by --output-dir after install, and the report directory must follow
    public static void SetRootProvider(Func<string> provider)
    {
        lock (Sync) _rootProvider = provider;
    }

    //Install installs the global exception handler, idempotent
    //reportType default report type, can be overridden per crash
    public static void Install(ReportType? reportType = null)
    {
        lock (Sync)
        {
            if (_installed) return;
            _reportType = reportType ?? ReportType.Crash;
            _installed = true;
        }
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        //Vanilla preloads the report path at startup so a report can still be built once the process is in a bad state
        CrashReport.Preload();
        //A stack overflow is the one failure the managed path cannot catch; the native layer reports that case instead
        NativeStackGuard.Install();
    }

    //CrashReportFilename report filename, maps to vanilla crash-<time>-<role>.txt
    public static string CrashReportFilename(string? role = null)
        => $"crash-{DateTime.Now:yyyy-MM-dd_HH.mm.ss}-{role ?? Role}.txt";

    //Handle handles one crash: builds the report, writes it to disk, returns the report path; returns null if it cannot be written
    public static string? Handle(Exception exception, string title, IReadOnlyList<string>? extraComments = null,
        ReportType? reportType = null)
        => Save(CrashReport.ForThrowable(exception, title), extraComments, reportType);

    //Save writes the report into crash-reports/ and logs the path
    //When writing fails, dumps the whole report into the log; the scene must not be lost to disk issues
    public static string? Save(CrashReport report, IReadOnlyList<string>? extraComments = null,
        ReportType? reportType = null)
    {
        var type = reportType ?? _reportType;
        var path = Path.Combine(CrashReportsDir, CrashReportFilename());
        if (!report.SaveToFile(path, type, extraComments))
        {
            Log.Critical($"Failed to write crash report. Report contents:{Environment.NewLine}{report.GetFriendlyReport(type, extraComments)}");
            return null;
        }
        Log.Critical($"Crash report written to {path}");
        return path;
    }

    //OnUnhandledException process-level uncaught exception
    //On this path the process is about to terminate: the report must be written out, then flush the log clean before returning control
    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is not Exception exception) return;
        var path = Handle(exception, "Uncaught exception", new[] { $"Fatal: {e.IsTerminating}" });
        Log.Exception(exception, path is null ? "Uncaught exception" : $"Uncaught exception, report {path}");
        Log.Flush();
    }

    //OnUnobservedTaskException unobserved task exception
    //This path does not terminate the process by default; writing a report for each would be overwhelming, so only the first is kept and the rest are just logged
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
            Log.Exception(e.Exception, "Another unobserved task exception, only the first report is kept");
            return;
        }
        var path = Handle(e.Exception, "Unobserved task exception");
        Log.Exception(e.Exception, path is null ? "Unobserved task exception" : $"Unobserved task exception, report {path}");
    }
}
