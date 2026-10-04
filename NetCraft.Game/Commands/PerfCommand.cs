using System.Diagnostics;
using NetCraft.Commands;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Server;

namespace NetCraft.Game.Commands;

//PerfCommand perf 命令对应原版 net.minecraft.server.commands.PerfCommand
//原版用 JFR 记录 NC 没有对应物 这里退化为进程 CPU 时间与 GC 分配的区间统计
public static class PerfCommand
{
    private static TimeSpan _startCpu;
    private static long _startAllocated;
    private static int _startGen0;
    private static int _startGen1;
    private static int _startGen2;
    private static long _startTicks;
    private static bool _recording;

    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("perf")
            .Requires(s => s.HasPermission(4))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("start")
                .Executes(Start))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("stop")
                .Executes(Stop)));
    }

    //Start 记下起点指标 重复开始直接覆盖
    private static int Start(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        using var process = Process.GetCurrentProcess();
        _startCpu = process.TotalProcessorTime;
        _startAllocated = GC.GetTotalAllocatedBytes();
        _startGen0 = GC.CollectionCount(0);
        _startGen1 = GC.CollectionCount(1);
        _startGen2 = GC.CollectionCount(2);
        _startTicks = Environment.TickCount64;
        _recording = true;
        //tick 阶段计时与这份区间统计同起同停 两块时钟都从这一刻算
        TickStageProfiler.Start();
        source.SendSuccess("已开始性能记录");
        return 1;
    }

    //Stop 输出区间内的 CPU 时间与分配量与各代回收次数
    private static int Stop(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        if (!_recording)
        {
            source.SendFailure("当前没有正在进行的性能记录");
            return 0;
        }

        using var process = Process.GetCurrentProcess();
        var cpu = process.TotalProcessorTime - _startCpu;
        var wall = (Environment.TickCount64 - _startTicks) / 1000.0;
        var allocated = GC.GetTotalAllocatedBytes() - _startAllocated;
        var gen0 = GC.CollectionCount(0) - _startGen0;
        var gen1 = GC.CollectionCount(1) - _startGen1;
        var gen2 = GC.CollectionCount(2) - _startGen2;
        _recording = false;
        TickStageProfiler.Stop();

        source.SendSuccess($"时长 {wall:F2} 秒 CPU 时间 {cpu.TotalSeconds:F2} 秒");
        source.SendSuccess($"托管分配 {allocated / 1048576.0:F1} MB");
        source.SendSuccess($"GC 次数 Gen0 {gen0} Gen1 {gen1} Gen2 {gen2}");
        //阶段表跟在总览后面 每行一条回执
        foreach (var line in TickStageProfiler.Report()) source.SendSuccess(line);
        return 1;
    }
}
