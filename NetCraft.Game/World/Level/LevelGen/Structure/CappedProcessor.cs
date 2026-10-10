using System.Buffers;
using NetCraft.Codec;
using NetCraft.Logging;
using NetCraft.Primitives;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//CappedProcessor capped processor, maps to vanilla CappedProcessor
//It does not process individual blocks; during finalize it randomly picks up to a limit of cells and hands them to the delegate
public sealed class CappedProcessor : StructureProcessor
{
    public static readonly MapCodec<CappedProcessor> MapCodec =
        RecordCodecBuilder.Of2<CappedProcessor, StructureProcessor, IntProvider>(
            StructureProcessorType.SingleCodec.FieldOf("delegate")
                .ForGetter<CappedProcessor, StructureProcessor>(c => c.Delegate),
            IntProviders.PositiveCodec.FieldOf("limit").ForGetter<CappedProcessor, IntProvider>(c => c.Limit),
            (delegateProcessor, limit) => new CappedProcessor(delegateProcessor, limit));

    public StructureProcessor Delegate { get; }
    public IntProvider Limit { get; }

    public CappedProcessor(StructureProcessor delegateProcessor, IntProvider limit)
    {
        Delegate = delegateProcessor;
        Limit = limit;
    }

    //EvaluatesEntirePieceState needs the whole piece state; finalize must not clip to the current chunk
    public bool EvaluatesEntirePieceState() => true;

    //FinalizeProcessing randomly replaces up to the limit of entries in processedBlockInfoList, maps to vanilla finalizeProcessing
    //Vanilla takes the random source from a positional fork of the level seed; since ServerLevel does not expose the seed here, fall back to the settings random source derived from the placement position
    public IReadOnlyList<StructureBlockInfo> FinalizeProcessing(WorldGenRegion? level, BlockPos position,
        BlockPos referencePos, IReadOnlyList<StructureBlockInfo> originalBlockInfoList,
        IReadOnlyList<StructureBlockInfo> processedBlockInfoList, StructurePlaceSettings settings)
    {
        if (Limit.MaxInclusive == 0 || processedBlockInfoList.Count == 0) return processedBlockInfoList;
        if (originalBlockInfoList.Count != processedBlockInfoList.Count)
        {
            Log.Warning($"CappedProcessor original and processed block tables are out of sync, skipping finalize (original {originalBlockInfoList.Count} processed {processedBlockInfoList.Count})");
            return processedBlockInfoList;
        }
        var random = settings.GetRandom(position);
        var maxToReplace = Math.Min(Limit.Sample(random), processedBlockInfoList.Count);
        if (maxToReplace < 1) return processedBlockInfoList;
        var count = processedBlockInfoList.Count;
        var indices = RentShuffledIndices(count, random);
        try
        {
            var result = processedBlockInfoList.ToList();
            var replaced = 0;
            for (var k = 0; k < count; k++)
            {
                if (replaced >= maxToReplace) break;
                var index = indices[k];
                var originalBlockInfo = originalBlockInfoList[index];
                var processedBlockInfo = result[index];
                var maybeAltered = Delegate.ProcessBlock(level, position, referencePos, originalBlockInfo.Pos,
                    processedBlockInfo, settings);
                if (maybeAltered is null || processedBlockInfo.Equals(maybeAltered.Value)) continue;
                replaced++;
                result[index] = maybeAltered.Value;
            }
            return result;
        }
        finally
        {
            ArrayPool<int>.Shared.Return(indices);
        }
    }

    //RentShuffledIndices shuffles the index sequence, maps to the back-to-front swap in vanilla Util.toShuffledList
    //The array is rented from the shared pool and owned by the caller, which returns it: a single piece can carry
    //hundreds of blocks and this runs once per piece that carries a capped processor, so a fresh array every call
    //turns into a System.Int32[] of tens of megabytes over a run. The pooled array may be longer than count, so the
    //caller must iterate count entries rather than the array length
    private static int[] RentShuffledIndices(int count, RandomSource random)
    {
        NetCraft.Util.SiteCounters.CountJigsawIndices(count);
        var indices = ArrayPool<int>.Shared.Rent(count);
        for (var i = 0; i < count; i++) indices[i] = i;
        for (var i = count - 1; i > 0; i--)
        {
            var j = random.NextInt(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }
        return indices;
    }
}
