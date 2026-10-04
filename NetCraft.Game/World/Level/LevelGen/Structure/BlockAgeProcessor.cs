using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Util.Random;
using RegBlock = NetCraft.Registry.Block;
using StateHalf = NetCraft.Registry.Enums.Half;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//BlockAgeProcessor 把石砖结构做旧 对应原版 BlockAgeProcessor
//按苔藓度决定换成苔藓变体 再按概率换成裂纹砖或随机朝向的楼梯
public sealed class BlockAgeProcessor : StructureProcessor
{
    public static readonly MapCodec<BlockAgeProcessor> MapCodec =
        new StructureSingleFieldMapCodec<BlockAgeProcessor, float>(
            Codecs.Float.FieldOf("mossiness"), m => new BlockAgeProcessor(m), p => p.Mossiness);

    private const float ProbabilityOfReplacingFullBlock = 0.5f;
    private const float ProbabilityOfReplacingStairs = 0.5f;
    private const float ProbabilityOfReplacingObsidian = 0.15f;

    private static readonly TagKey<RegBlock> StairsTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("stairs"));

    private static readonly TagKey<RegBlock> SlabsTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("slabs"));

    private static readonly TagKey<RegBlock> WallsTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("walls"));

    //水平四个朝向 顺序与原版 Direction.Plane.HORIZONTAL 一致
    private static readonly Direction[] HorizontalDirections =
        { Direction.North, Direction.East, Direction.South, Direction.West };

    private static BlockState[]? _nonMossyReplacements;

    //NonMossyReplacements 非苔藓变体的两个候选 对应原版 NON_MOSSY_REPLACEMENTS
    private static BlockState[] NonMossyReplacements
        => _nonMossyReplacements ??= new[]
        {
            ProcessorBlockHelper.StateOf("stone_slab"),
            ProcessorBlockHelper.StateOf("stone_brick_slab"),
        };

    public float Mossiness { get; }

    public BlockAgeProcessor(float mossiness) => Mossiness = mossiness;

    public StructureBlockInfo? ProcessBlock(WorldGenRegion? level, BlockPos targetPosition, BlockPos referencePos,
        BlockPos templateRelativePos, StructureBlockInfo processedBlockInfo, StructurePlaceSettings settings)
    {
        var random = settings.GetRandom(processedBlockInfo.Pos);
        var state = processedBlockInfo.State;
        BlockState? newState = null;
        if (ProcessorBlockHelper.HasBlock(state, "stone_bricks")
            || ProcessorBlockHelper.HasBlock(state, "stone")
            || ProcessorBlockHelper.HasBlock(state, "chiseled_stone_bricks"))
            newState = MaybeReplaceFullStoneBlock(random);
        else if (ProcessorBlockHelper.InTag(state, StairsTag))
            newState = MaybeReplaceStairs(state, random);
        else if (ProcessorBlockHelper.InTag(state, SlabsTag))
            newState = MaybeReplaceSlab(state, random);
        else if (ProcessorBlockHelper.InTag(state, WallsTag))
            newState = MaybeReplaceWall(state, random);
        else if (ProcessorBlockHelper.HasBlock(state, "obsidian"))
            newState = MaybeReplaceObsidian(random);
        if (newState is null || newState.Value.Id == 0) return processedBlockInfo;
        return new StructureBlockInfo(processedBlockInfo.Pos, newState.Value, processedBlockInfo.Nbt);
    }

    //MaybeReplaceFullStoneBlock 整块石砖的替换 对应原版 maybeReplaceFullStoneBlock
    private BlockState? MaybeReplaceFullStoneBlock(RandomSource random)
    {
        if (random.NextFloat() >= ProbabilityOfReplacingFullBlock) return null;
        var nonMossy = new[]
        {
            ProcessorBlockHelper.StateOf("cracked_stone_bricks"),
            RandomFacingStairs(random, "stone_brick_stairs"),
        };
        var mossy = new[]
        {
            ProcessorBlockHelper.StateOf("mossy_stone_bricks"),
            RandomFacingStairs(random, "mossy_stone_brick_stairs"),
        };
        return GetRandomBlock(random, nonMossy, mossy);
    }

    //MaybeReplaceStairs 楼梯的替换 对应原版 maybeReplaceStairs
    private BlockState? MaybeReplaceStairs(BlockState blockState, RandomSource random)
    {
        if (random.NextFloat() >= ProbabilityOfReplacingStairs) return null;
        var mossy = new[]
        {
            ProcessorBlockHelper.CopyProperties(blockState, ProcessorBlockHelper.StateOf("mossy_stone_brick_stairs")),
            ProcessorBlockHelper.StateOf("mossy_stone_brick_slab"),
        };
        return GetRandomBlock(random, NonMossyReplacements, mossy);
    }

    //MaybeReplaceSlab 台阶的替换 对应原版 maybeReplaceSlab
    private BlockState? MaybeReplaceSlab(BlockState blockState, RandomSource random)
        => random.NextFloat() < Mossiness
            ? ProcessorBlockHelper.CopyProperties(blockState, ProcessorBlockHelper.StateOf("mossy_stone_brick_slab"))
            : null;

    //MaybeReplaceWall 墙的替换 对应原版 maybeReplaceWall
    private BlockState? MaybeReplaceWall(BlockState blockState, RandomSource random)
        => random.NextFloat() < Mossiness
            ? ProcessorBlockHelper.CopyProperties(blockState, ProcessorBlockHelper.StateOf("mossy_stone_brick_wall"))
            : null;

    //MaybeReplaceObsidian 黑曜石的替换 对应原版 maybeReplaceObsidian
    private BlockState? MaybeReplaceObsidian(RandomSource random)
        => random.NextFloat() < ProbabilityOfReplacingObsidian
            ? ProcessorBlockHelper.StateOf("crying_obsidian")
            : null;

    //RandomFacingStairs 随机朝向与半砖位置的楼梯状态 对应原版 getRandomFacingStairs
    private static BlockState RandomFacingStairs(RandomSource random, string path)
    {
        var state = ProcessorBlockHelper.StateOf(path);
        if (state.Id == 0) return state;
        var facing = HorizontalDirections[random.NextInt(HorizontalDirections.Length)];
        var facingProperty = ProcessorBlockHelper.FindProperty(state, "facing");
        if (facingProperty is not null)
            state = StructureBlockTransforms.SetIfAllowed(state, facingProperty, facing.ToState());
        var halfProperty = ProcessorBlockHelper.FindProperty(state, "half");
        if (halfProperty is not null)
            state = StructureBlockTransforms.SetIfAllowed(state, halfProperty,
                random.NextInt(2) == 0 ? StateHalf.top : StateHalf.bottom);
        return state;
    }

    //GetRandomBlock 按苔藓度在两组候选间挑一组 对应原版 getRandomBlock(random, nonMossy, mossy)
    private BlockState GetRandomBlock(RandomSource random, BlockState[] nonMossyBlocks, BlockState[] mossyBlocks)
        => random.NextFloat() < Mossiness
            ? mossyBlocks[random.NextInt(mossyBlocks.Length)]
            : nonMossyBlocks[random.NextInt(nonMossyBlocks.Length)];
}
