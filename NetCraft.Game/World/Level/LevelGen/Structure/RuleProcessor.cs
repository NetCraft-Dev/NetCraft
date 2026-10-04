using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Util;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//RuleProcessor 规则处理器 对应原版 RuleProcessor
//逐条跑规则 第一条命中的输出状态顶替原方块 全不命中原样返回
public sealed class RuleProcessor : StructureProcessor
{
    public static readonly MapCodec<RuleProcessor> MapCodec =
        new StructureSingleFieldMapCodec<RuleProcessor, IReadOnlyList<ProcessorRule>>(
            ProcessorRule.Codec.ListOf().FieldOf("rules"),
            rules => new RuleProcessor(rules),
            p => p.Rules);

    public IReadOnlyList<ProcessorRule> Rules { get; }

    public RuleProcessor(IReadOnlyList<ProcessorRule> rules) => Rules = rules;

    public StructureBlockInfo? ProcessBlock(WorldGenRegion? level, BlockPos targetPosition, BlockPos referencePos,
        BlockPos templateRelativePos, StructureBlockInfo processedBlockInfo, StructurePlaceSettings settings)
    {
        //原版用方块坐标派生的种子 与放置坐标无关 同一格每次放置结果一致
        var random = RandomSource.Create(Mth.GetSeed(processedBlockInfo.Pos.X, processedBlockInfo.Pos.Y, processedBlockInfo.Pos.Z));
        foreach (var rule in Rules)
        {
            if (!rule.Test(level, processedBlockInfo.State, templateRelativePos, processedBlockInfo.Pos, referencePos, random))
                continue;
            return new StructureBlockInfo(processedBlockInfo.Pos, rule.OutputState,
                rule.GetOutputTag(random, processedBlockInfo.Nbt));
        }
        return processedBlockInfo;
    }

    public MapCodec<StructureProcessor> ElementCodec =>
        new ProcessorMapCodec<RuleProcessor, StructureProcessor>(MapCodec);
}
