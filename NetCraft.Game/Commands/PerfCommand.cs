using System.Diagnostics;
using NetCraft.Commands;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Server;

namespace NetCraft.Game.Commands;

//PerfCommand perf command, maps to vanilla net.minecraft.server.commands.PerfCommand
//Vanilla uses JFR; NC has no equivalent, so this degrades to interval statistics of process CPU time and GC allocation
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

    //Start records the starting metrics; a repeated start just overwrites
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
        //The tick stage timing starts and stops with this interval stat; both clocks count from this moment
        TickStageProfiler.Start();
        source.SendSuccess("performance recording started");
        return 1;
    }

    //Stop prints the interval's CPU time, allocation and per-generation collection counts
    private static int Stop(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        if (!_recording)
        {
            source.SendFailure("there is no performance recording in progress");
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

        source.SendSuccess($"duration {wall:F2} s, CPU time {cpu.TotalSeconds:F2} s");
        source.SendSuccess($"managed allocation {allocated / 1048576.0:F1} MB");
        source.SendSuccess($"GC counts Gen0 {gen0} Gen1 {gen1} Gen2 {gen2}");
        //The stage table follows the overview, one reply per line
        foreach (var line in TickStageProfiler.Report()) source.SendSuccess(line);
        return 1;
    }
}
