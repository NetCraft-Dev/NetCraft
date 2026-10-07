using System.Diagnostics;

namespace NetCraft.Game.Server;

//TickStageProfiler tick stage timing, accumulating per-tick stage costs for the perf command's share table
//Off by default; when off Now returns 0 and Record is skipped, adding only one check to the whole path
//With multiple dimensions the same stage is recorded several times; the accumulated value is averaged by tick count, reporting the per-tick cost summed over all dimensions
public static class TickStageProfiler
{
    //StageCount the number of stages, aligned with the last TickStage enum value
    public const int StageCount = (int)TickStage.Count;

    //Conversion factor from stopwatch ticks to nanoseconds
    private static readonly double NanosecondsPerTimestampTick = 1_000_000_000.0 / Stopwatch.Frequency;

    private static readonly long[] Totals = new long[StageCount];
    private static int _ticks;
    private static bool _enabled;

    public static bool Enabled => _enabled;

    //Start clears the accumulation and starts the corresponding perf start
    public static void Start()
    {
        Array.Clear(Totals);
        _ticks = 0;
        _enabled = true;
    }

    //Stop stops accumulating; existing data is kept for the report
    public static void Stop() => _enabled = false;

    //EndTick ends one tick; the share is averaged by tick count
    public static void EndTick()
    {
        if (_enabled) _ticks++;
    }

    //Now takes the timing start; returns 0 when disabled, which Record recognizes and skips
    public static long Now() => _enabled ? Stopwatch.GetTimestamp() : 0;

    //Record records a duration; start 0 means disabled
    public static void Record(TickStage stage, long start)
    {
        if (start != 0) Totals[(int)stage] += Stopwatch.GetTimestamp() - start;
    }

    //Report prints each stage's per-tick average microseconds and share, skipping stages never entered
    //The share denominator is the Tick body time; the difference from the sum of parts is the part not covered by any stage
    public static IReadOnlyList<string> Report()
    {
        var lines = new List<string>(StageCount + 3);
        var ticks = Math.Max(1, _ticks);
        var tickTotal = Totals[(int)TickStage.TickTotal];
        var body = 0L;
        for (var i = 0; i < StageCount; i++)
        {
            if (i != (int)TickStage.TickTotal) body += Totals[i];
        }
        var basis = tickTotal > 0 ? tickTotal : body;

            lines.Add($"tick stage time, {_ticks} tick average, in microseconds");
        for (var i = 0; i < StageCount; i++)
        {
            if (Totals[i] == 0) continue;
            var micros = Totals[i] * NanosecondsPerTimestampTick / 1000.0 / ticks;
            var share = basis == 0 ? 0 : Totals[i] * 100.0 / basis;
            lines.Add($"  {(TickStage)i,-16}{micros,10:F1} {share,6:F1}%");
        }

        var bodyMicros = body * NanosecondsPerTimestampTick / 1000.0 / ticks;
        var bodyShare = basis == 0 ? 0 : body * 100.0 / basis;
            lines.Add($"  {"parts total",-16}{bodyMicros,10:F1} {bodyShare,6:F1}%");
        return lines;
    }
}

    //TickStage the timing segments within a tick, mapping one to one to the sections of DedicatedServer.Tick
    //TickTotal is the Tick body total; comparing it with the sum of parts shows whether a large chunk of work is missing from the parts
public enum TickStage
{
    TickTotal,
    Console,
    Connections,
    Clock,
    BlockTicks,
    FluidTicks,
    LevelTick,
    FlushBlocks,
    RandomTick,
    BlockEvents,
    Light,
    EntityInside,
    BlockEntities,
    Players,
    DebugPlayers,
    EntityTracking,
    AutoSave,
    Count,
}
