using NetCraft.Codec;
using NetCraft.Logging;
using NetCraft.Primitives;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//CappedProcessor 限次处理器 对应原版 CappedProcessor
//自己不处理单个方块 只在收尾阶段按上限随机挑若干格交给 delegate 处理
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

    //EvaluatesEntirePieceState 要看整片结构状态 收尾阶段不能按当前区块裁剪
    public bool EvaluatesEntirePieceState() => true;

    //FinalizeProcessing 按上限随机替换 processedBlockInfoList 里的若干项 对应原版 finalizeProcessing
    //原版随机源取自关卡种子的位置化派生 本作 ServerLevel 未暴露种子 退回按放置坐标派生的设置随机源
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
        var indices = ShuffledIndices(processedBlockInfoList.Count, random);
        var result = processedBlockInfoList.ToList();
        var replaced = 0;
        foreach (var index in indices)
        {
            if (replaced >= maxToReplace) break;
            var originalBlockInfo = originalBlockInfoList[index];
            var processedBlockInfo = result[index];
            var maybeAltered = Delegate.ProcessBlock(level, position, referencePos, originalBlockInfo.Pos,
                processedBlockInfo, settings);
            if (maybeAltered is null || processedBlockInfo.Equals(maybeAltered)) continue;
            replaced++;
            result[index] = maybeAltered;
        }
        return result;
    }

    //ShuffledIndices 洗牌索引序列 对应原版 Util.toShuffledList 的从后往前交换法
    private static List<int> ShuffledIndices(int count, RandomSource random)
    {
        var indices = new List<int>(count);
        for (var i = 0; i < count; i++) indices.Add(i);
        for (var i = count - 1; i > 0; i--)
        {
            var j = random.NextInt(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }
        return indices;
    }
}
