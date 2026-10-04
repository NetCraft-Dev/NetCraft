namespace NetCraft.Util;

//包装CrashReport的异常对应原版net.minecraft.ReportedException
//在需要抛出带上下文的错误时使用
public class ReportedException : Exception
{
    public CrashReport Report { get; }

    public ReportedException(CrashReport report) : base(report.Title, report.Exception)
    {
        Report = report;
    }

    public override string Message => Report.Title;

    public override Exception? GetBaseException()
    {
        return Report.Exception is ReportedException innerReported
            ? innerReported.GetBaseException()
            : this;
    }
}
