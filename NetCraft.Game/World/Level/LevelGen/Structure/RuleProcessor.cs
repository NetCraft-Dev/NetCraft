using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Util;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//RuleProcessor rule processor, maps to vanilla RuleProcessor
//Runs rules in order; the output state of the first hit replaces the block, and nothing matching returns the block unchanged
public sealed class RuleProcessor : StructureProcessor
{
    public static readonly MapCodec<RuleProcessor> MapCodec =
        new StructureSingleFieldMapCodec<RuleProcessor, IReadOnlyList<ProcessorRule>>(
            ProcessorRule.Codec.ListOf().FieldOf("rules"),
            rules => new RuleProcessor(rules),
            p => p.Rules);

    public IReadOnlyList<ProcessorRule> Rules { get; }

    //ModifiesBlockEntityData only an append_static rule writes into the tag it is handed, since it merges its data into
    //it; passthrough returns the same tag untouched and clear and append_loot build a new one, so only this case needs
    //the chain to work on an isolated copy of the template's tag
    public bool ModifiesBlockEntityData { get; }

    public RuleProcessor(IReadOnlyList<ProcessorRule> rules)
    {
        Rules = rules;
        for (var i = 0; i < rules.Count; i++)
        {
            if (rules[i].BlockEntityModifier is not AppendStaticModifier) continue;
            ModifiesBlockEntityData = true;
            break;
        }
    }

    public StructureBlockInfo? ProcessBlock(WorldGenRegion? level, BlockPos targetPosition, BlockPos referencePos,
        BlockPos templateRelativePos, StructureBlockInfo processedBlockInfo, StructurePlaceSettings settings)
    {
        //Vanilla derives the seed from block coords, independent of placement position, so the same cell is deterministic per placement
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
