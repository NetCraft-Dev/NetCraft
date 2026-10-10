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
        //Cleared alongside the tick totals so a window reports only its own entity tracking numbers
        EntityTracker.ResetDiagnostics();
        ChunkSender.DiagnosticUpdateCenterCalls = 0;
        ChunkSender.DiagnosticCenterMoves = 0;
        ChunkSender.DiagnosticChunksQueued = 0;
        ChunkSender.DiagnosticTickClosed = 0;
        ChunkSender.DiagnosticTickDisconnected = 0;
        ChunkSender.DiagnosticTickEmpty = 0;
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

    //IsNested reports stages that measure time already counted by an outer stage, excluded from the parts total
    //Without this the chunk-sending breakdown is added to the body a second time and parts total climbs past 100%
    private static bool IsNested(TickStage stage)
        => (stage >= TickStage.ChunkScan && stage <= TickStage.ChunkSend)
           || (stage >= TickStage.TrackPrep && stage <= TickStage.TrackSend);

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
            if (i == (int)TickStage.TickTotal || IsNested((TickStage)i)) continue;
            body += Totals[i];
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

        //Temporary: entity tracking scaling, read next to the table above
        //scanned per player is what one player walks past per tick, visible per player how much of that the distance test keeps
        if (EntityTracker.DiagnosticPlayerCount > 0)
        {
            var scanned = (double)EntityTracker.DiagnosticNearbyTotal / EntityTracker.DiagnosticPlayerCount;
            var visible = (double)EntityTracker.DiagnosticVisibleTotal / EntityTracker.DiagnosticPlayerCount;
            var packets = (double)EntityTracker.DiagnosticPacketTotal / _ticks;
            lines.Add($"  entity tracking: candidates {EntityTracker.DiagnosticEntities}, scanned per player {scanned:F1}, visible per player {visible:F1}, packets per tick {packets:F1}");
            var kinds = string.Join(' ', EntityTracker.DiagnosticPacketNames
                .Select((name, i) => $"{name}={EntityTracker.DiagnosticPacketKinds[i] / (double)ticks:F0}"));
            lines.Add($"  entity tracking packets per tick: {kinds}");
            //Sync rounds per tick versus the player count per tick: these must match, and a mismatch means the per-player figures above divide by the wrong denominator
            lines.Add($"  entity tracking sync rounds per tick: {EntityTracker.DiagnosticSyncCalls / (double)ticks:F1}, players per tick: {EntityTracker.DiagnosticPlayerCount / (double)ticks:F1}");
        }
        //Temporary: whether chunk sending had any work at all, and where it stopped if it did not
        //Calls with no move means the player never changed chunk; moves with nothing queued means the view circle was already covered
        lines.Add($"  chunk sending: update calls per tick {ChunkSender.DiagnosticUpdateCenterCalls / (double)ticks:F1}, center moves per tick {ChunkSender.DiagnosticCenterMoves / (double)ticks:F1}, queued per tick {ChunkSender.DiagnosticChunksQueued / (double)ticks:F1}");
        lines.Add($"  chunk sender exits per tick: closed {ChunkSender.DiagnosticTickClosed / (double)ticks:F1}, disconnected {ChunkSender.DiagnosticTickDisconnected / (double)ticks:F1}, empty queue {ChunkSender.DiagnosticTickEmpty / (double)ticks:F1}");
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
    //ChunkSender breakdown, temporary instrumentation: splits the Players stage to show where chunk sending spends its time
    //ChunkScan is picking the batch out of the pending queue, including the provider lookup and the light-ready test that defers a chunk
    //ChunkPrep is gathering block entities and building the packet object
    //ChunkPrepChunk/ChunkPrepLight split that further: which half of the payload actually costs the time
    //TrackLoop is the per-candidate loop body, TrackPrune the stale-pair sweep that follows it
    //Keep these contiguous, IsNested relies on the range
    //ChunkSend is the send itself, which only enqueues because the payload is already bytes
    //Keep these contiguous, IsNested relies on the range
    ChunkScan,
    ChunkPrep,
    ChunkPrepChunk,
    //Inside the chunk-body write: PrepSect is the 24-section loop plus the temporary buffer it fills,
    //PrepHdr is copying that buffer into the packet body, PrepBe is the block entity entries
    ChunkPrepHdr,
    ChunkPrepSect,
    ChunkPrepBe,
    ChunkPrepLight,
    ChunkSend,
    //EntityTracker breakdown, temporary instrumentation split the same way and nested for the same reason
    //TrackPrep is collecting candidates and bucketing them, TrackSync is per-player nearby gathering plus packet building,
    //TrackSend is handing those packets to the connection, which only enqueues them
    TrackPrep,
    TrackSync,
    TrackLoop,
    TrackPrune,
    TrackSend,
    Count,
}
