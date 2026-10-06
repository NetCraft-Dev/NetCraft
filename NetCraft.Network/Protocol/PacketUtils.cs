using NetCraft.Logging;

namespace NetCraft.Network.Protocol;

//PacketUtils packet utilities, maps to vanilla net.minecraft.network.protocol.PacketUtils
//The simplified form omits ServerLevel/CrashReport dependencies and keeps only the EnsureRunningOnSameThread core logic
public static class PacketUtils
{
    //EnsureRunningOnSameThread checks whether the current thread is the same as the handler's
    //When different it schedules to the main thread and throws to interrupt the current handling
    //Aligns with vanilla ensureRunningOnSameThread but throws InvalidOperationException instead of RunningOnDifferentThreadException
    public static void EnsureRunningOnSameThread<THandler>(
        Packet<THandler> packet,
        THandler listener,
        PacketProcessor processor)
        where THandler : class
    {
        if (!processor.IsSameThread)
        {
            processor.ScheduleIfPossible(listener, packet);
            throw new InvalidOperationException("packet scheduled for main-thread handling");
        }
    }

    //MakeReportedException wraps an exception as InvalidOperationException, aligns with vanilla makeReportedException
    //The simplified form does not build a full CrashReport and only keeps the exception chain
    public static Exception MakeReportedException<THandler>(
        Exception cause,
        Packet<THandler> packet,
        THandler listener)
        where THandler : class
    {
        //Vanilla wraps into ReportedException with a full CrashReport
        //The simplified form wraps with InvalidOperationException, preserving the original exception
        return new InvalidOperationException(
            $"packet handling failed listener={typeof(THandler).Name} packet={packet.GetType().Name}",
            cause);
    }

    //FillCrashReport fills the crash report, aligns with vanilla fillCrashReport
    //The simplified form only logs and does not build a full CrashReport
    public static void FillCrashReport<THandler>(
        Exception report,
        THandler listener,
        Packet<THandler> packet)
        where THandler : class
    {
        Log.Error("PacketUtils", $"Packet handling crash listener={typeof(THandler).Name} packet={packet.GetType().Name} packetType={packet.Type}");
        Log.Error("PacketUtils", report.ToString());
    }
}
