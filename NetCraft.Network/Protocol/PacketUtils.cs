using NetCraft.Logging;

namespace NetCraft.Network.Protocol;

//PacketUtils 包工具类对应原版 net.minecraft.network.protocol.PacketUtils
//简化版省略 ServerLevel/CrashReport 依赖只保留 EnsureRunningOnSameThread 核心逻辑
public static class PacketUtils
{
    //EnsureRunningOnSameThread 检查当前线程是否与处理器相同
    //不同则调度到主线程并抛异常中断当前处理
    //对齐原版 ensureRunningOnSameThread 但抛 InvalidOperationException 替代 RunningOnDifferentThreadException
    public static void EnsureRunningOnSameThread<THandler>(
        Packet<THandler> packet,
        THandler listener,
        PacketProcessor processor)
        where THandler : class
    {
        if (!processor.IsSameThread)
        {
            processor.ScheduleIfPossible(listener, packet);
            throw new InvalidOperationException("包调度到主线程处理");
        }
    }

    //MakeReportedException 包装异常为 InvalidOperationException 对齐原版 makeReportedException
    //简化版不构造完整 CrashReport 只保留异常链
    public static Exception MakeReportedException<THandler>(
        Exception cause,
        Packet<THandler> packet,
        THandler listener)
        where THandler : class
    {
        //原版包装为 ReportedException 含完整 CrashReport
        //简化版用 InvalidOperationException 包装保留原异常
        return new InvalidOperationException(
            $"包处理失败 listener={typeof(THandler).Name} packet={packet.GetType().Name}",
            cause);
    }

    //FillCrashReport 填充崩溃报告对齐原版 fillCrashReport
    //简化版只记录日志不构造完整 CrashReport
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
