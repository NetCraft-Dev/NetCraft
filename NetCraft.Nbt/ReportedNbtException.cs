using NetCraft.Util;

namespace NetCraft.Nbt;

//ReportedNbtException, mirroring vanilla net.minecraft.nbt.ReportedNbtException
//Extends ReportedException to wrap a CrashReport and keep the crash context
public sealed class ReportedNbtException : ReportedException
{
    public ReportedNbtException(CrashReport report) : base(report) { }
}
