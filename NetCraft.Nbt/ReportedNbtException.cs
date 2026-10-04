using NetCraft.Util;

namespace NetCraft.Nbt;

//ReportedNbtException对应原版net.minecraft.nbt.ReportedNbtException
//继承ReportedException包装CrashReport保留崩溃上下文
public sealed class ReportedNbtException : ReportedException
{
    public ReportedNbtException(CrashReport report) : base(report) { }
}
