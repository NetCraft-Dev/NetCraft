using NetCraft.Logging;

namespace NetCraft.Util;

//Temporary instrumentation, not a permanent facility: counts Holder<T>.Direct allocations per call site so the hot
//one can be told apart. The sample profiler cannot do this because the one line factory gets inlined away, so the
//constructor frame never reaches the stack. Delete this file and its increments once the counts have been read
public static class SiteCounters
{
    private const long ReportEvery = 250_000;

    public static long BiomeChunkStatus;
    public static long MappedRegistryMiss;
    public static long HolderLookupMiss;

    //CountBiomeChunkStatus the BIOME stage fill, one call per quart cell per section
    //The mod side cannot read these: mods compile against the published kernel, which does not have this file yet,
    //so each counter reports itself every quarter million calls and the log line carries the running total
    public static void CountBiomeChunkStatus() => Report(Interlocked.Increment(ref BiomeChunkStatus), "chunkStatus");

    //CountMappedRegistryMiss WrapAsHolder found no holder for a value and had to build one
    public static void CountMappedRegistryMiss() => Report(Interlocked.Increment(ref MappedRegistryMiss), "mappedRegistryMiss");

    //CountHolderLookupMiss GetOrDefault found no holder for the key and had to build one
    public static void CountHolderLookupMiss() => Report(Interlocked.Increment(ref HolderLookupMiss), "holderLookupMiss");

    //Report prints a running total every quarter million calls
    private static void Report(long count, string site)
    {
        if (count % ReportEvery == 0) Log.Info($"[site] directBiome {site}={count}");
    }

    private static long _wrappedPeak;

    //Wrapped tracks the largest memo size seen, because the capacity handed to those dictionaries has to clear this
    //peak. Reporting it on the stage line instead of on every growth keeps the hot path silent
    public static void Wrapped(string site, int count)
    {
        var peak = Interlocked.Read(ref _wrappedPeak);
        while (count > peak)
        {
            var seen = Interlocked.CompareExchange(ref _wrappedPeak, count, peak);
            if (seen == peak) return;
            peak = seen;
        }
    }

    public static long JigsawIndexCells;
    private static long _jigsawIndexReported;

    //CountJigsawIndices adds up the index buffers CappedProcessor builds for its shuffle, one per piece that carries a
    //capped processor. The running total times four bytes is what those buffers cost, which is how the unnamed
    //System.Int32[] on the allocation profile gets attributed
    //Unlike the other counters this one adds a variable amount rather than one per call, so it reports on crossing a
    //multiple of ReportEvery instead of landing exactly on one
    public static void CountJigsawIndices(int count)
    {
        var total = Interlocked.Add(ref JigsawIndexCells, count);
        if (total - Interlocked.Read(ref _jigsawIndexReported) < ReportEvery) return;
        Interlocked.Exchange(ref _jigsawIndexReported, total);
        Log.Info($"[site] directBiome jigsawIndexCells={total}");
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, long> HolderDirects = new();
    private static long _holderDirectTotal;
    private static long _holderDirectReported;

    //CountHolderDirect counts Holder<T>.Direct by type name. The allocation profile reports a large Direct<Biome>
    //traffic whose call sites do not add up, so the count has to come from the construction itself
    public static void CountHolderDirect(string type)
    {
        HolderDirects.AddOrUpdate(type, 1, (_, v) => v + 1);
        var total = Interlocked.Increment(ref _holderDirectTotal);
        if (total - Interlocked.Read(ref _holderDirectReported) < ReportEvery) return;
        Interlocked.Exchange(ref _holderDirectReported, total);
        var parts = new List<string>();
        foreach (var pair in HolderDirects) parts.Add($"{pair.Key}={pair.Value}");
        Log.Info($"[site] holderDirect total={total} " + string.Join(" ", parts));
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, long> StageBytes = new();
    private static long _stageSteps;
    private const long StageReportEvery = 500;

    //Stage accumulates the bytes a generation status added, measured as the difference of GC.GetTotalAllocatedBytes
    //around it. The allocation profile says which type grows but never which stage produced it, and allocation ticks
    //carry no stack, so this is the only way to place a type. Every StageReportEvery steps the running totals print
    public static void Stage(string stage, long bytes)
    {
        StageBytes.AddOrUpdate(stage, bytes, (_, v) => v + bytes);
        if (Interlocked.Increment(ref _stageSteps) % StageReportEvery != 0) return;
        var parts = new List<string>();
        foreach (var pair in StageBytes) parts.Add($"{pair.Key}={pair.Value / 1048576.0:F1}");
        Log.Info("[site] stageAlloc cumulative MiB: " + string.Join(" ", parts)
            + $" wrappedPeak={Interlocked.Read(ref _wrappedPeak)}");
    }
}
