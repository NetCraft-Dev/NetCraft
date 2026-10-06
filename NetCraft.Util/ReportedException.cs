namespace NetCraft.Util;

//Exception wrapping a CrashReport, maps to vanilla net.minecraft.ReportedException
//Used when an error with context needs to be thrown
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
