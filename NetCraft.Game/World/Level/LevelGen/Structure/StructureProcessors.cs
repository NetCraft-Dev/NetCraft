using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using RegBlock = NetCraft.Registry.Block;
using HeightmapTypes = NetCraft.Registry.Heightmap.Types;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//NopProcessor 空处理器 对应原版 NopProcessor 什么都不改
public sealed class NopProcessor : StructureProcessor
{
    public static readonly NopProcessor Instance = new();

    public static readonly MapCodec<NopProcessor> MapCodec = new StructureUnitMapCodec<NopProcessor>(() => Instance);

    private NopProcessor() { }

    public MapCodec<StructureProcessor> ElementCodec =>
        new ProcessorMapCodec<NopProcessor, StructureProcessor>(MapCodec);
}

//BlockIgnoreProcessor 忽略指定方块 对应原版 BlockIgnoreProcessor 命中的格子直接丢弃
public sealed class BlockIgnoreProcessor : StructureProcessor
{
    //blocks 字段按原版是方块状态列表 这里只取方块本身
    public static readonly MapCodec<BlockIgnoreProcessor> MapCodec =
        new StructureSingleFieldMapCodec<BlockIgnoreProcessor, IReadOnlyList<BlockState>>(
            BlockStateCodec.Instance.ListOf().FieldOf("blocks"),
            states => new BlockIgnoreProcessor(states.Select(s => s.Owner).ToList()),
            p => p.ToIgnore.Select(b => b.DefaultBlockState).ToList());

    private static BlockIgnoreProcessor? _structureBlock;
    private static BlockIgnoreProcessor? _air;
    private static BlockIgnoreProcessor? _structureAndAir;

    //StructureBlock 只忽略结构方块 对应原版 STRUCTURE_BLOCK
    public static BlockIgnoreProcessor StructureBlock
        => _structureBlock ??= new BlockIgnoreProcessor(new[] { ProcessorBlockHelper.BlockOf("structure_block")! });

    //Air 只忽略空气 对应原版 AIR
    public static BlockIgnoreProcessor Air
        => _air ??= new BlockIgnoreProcessor(new[] { ProcessorBlockHelper.BlockOf("air")! });

    //StructureAndAir 忽略空气与结构方块 对应原版 STRUCTURE_AND_AIR
    public static BlockIgnoreProcessor StructureAndAir
        => _structureAndAir ??= new BlockIgnoreProcessor(new[]
        {
            ProcessorBlockHelper.BlockOf("air")!,
            ProcessorBlockHelper.BlockOf("structure_block")!,
        });

    public IReadOnlyList<RegBlock> ToIgnore { get; }

    public BlockIgnoreProcessor(IReadOnlyList<RegBlock> toIgnore) => ToIgnore = toIgnore;

    public StructureBlockInfo? ProcessBlock(WorldGenRegion? level, BlockPos targetPosition, BlockPos referencePos,
        BlockPos templateRelativePos, StructureBlockInfo processedBlockInfo, StructurePlaceSettings settings)
        => ToIgnore.Contains(processedBlockInfo.State.Owner) ? null : processedBlockInfo;

    public MapCodec<StructureProcessor> ElementCodec =>
        new ProcessorMapCodec<BlockIgnoreProcessor, StructureProcessor>(MapCodec);
}

//BlockRotProcessor 按完整度随机丢弃方块 对应原版 BlockRotProcessor
public sealed class BlockRotProcessor : StructureProcessor
{
    public static readonly MapCodec<BlockRotProcessor> MapCodec =
        RecordCodecBuilder.Of2<BlockRotProcessor, Optional<HolderSet<RegBlock>>, float>(
            HolderSetCodecs.BlockSet.OptionalFieldOf("rottable_blocks")
                .ForGetter<BlockRotProcessor, Optional<HolderSet<RegBlock>>>(p => p.RottableBlocks),
            Codecs.Float.FieldOf("integrity").ForGetter<BlockRotProcessor, float>(p => p.Integrity),
            (rottableBlocks, integrity) => new BlockRotProcessor(rottableBlocks, integrity));

    public Optional<HolderSet<RegBlock>> RottableBlocks { get; }
    public float Integrity { get; }

    public BlockRotProcessor(HolderSet<RegBlock> rottableBlocks, float integrity)
        : this(Optional<HolderSet<RegBlock>>.Of(rottableBlocks), integrity)
    {
    }

    public BlockRotProcessor(float integrity) : this(Optional<HolderSet<RegBlock>>.Empty(), integrity) { }

    public BlockRotProcessor(Optional<HolderSet<RegBlock>> rottableBlocks, float integrity)
    {
        RottableBlocks = rottableBlocks;
        Integrity = integrity;
    }

    public StructureBlockInfo? ProcessBlock(WorldGenRegion? level, BlockPos targetPosition, BlockPos referencePos,
        BlockPos templateRelativePos, StructureBlockInfo processedBlockInfo, StructurePlaceSettings settings)
    {
        var random = settings.GetRandom(processedBlockInfo.Pos);
        if ((RottableBlocks.IsPresent && !ProcessorBlockHelper.InSet(processedBlockInfo.State, RottableBlocks.Get()))
            || random.NextFloat() <= Integrity)
            return processedBlockInfo;
        return null;
    }

    public MapCodec<StructureProcessor> ElementCodec =>
        new ProcessorMapCodec<BlockRotProcessor, StructureProcessor>(MapCodec);
}

//GravityProcessor 把方块贴到高度图上 对应原版 GravityProcessor
//没有世界视图时求不出高度 原版这条路径必定有 level 这里原样保留
public sealed class GravityProcessor : StructureProcessor
{
    public static readonly MapCodec<GravityProcessor> MapCodec =
        RecordCodecBuilder.Of2<GravityProcessor, HeightmapTypes, int>(
            StructureHeightmapTypeCodec.Instance.OptionalFieldOf("heightmap", HeightmapTypes.WorldSurfaceWg)
                .ForGetter<GravityProcessor, HeightmapTypes>(p => p.Heightmap),
            Codecs.Int.OptionalFieldOf("offset", 0).ForGetter<GravityProcessor, int>(p => p.Offset),
            (heightmap, offset) => new GravityProcessor(heightmap, offset));

    public HeightmapTypes Heightmap { get; }
    public int Offset { get; }

    public GravityProcessor(HeightmapTypes heightmap, int offset)
    {
        Heightmap = heightmap;
        Offset = offset;
    }

    public StructureBlockInfo? ProcessBlock(WorldGenRegion? level, BlockPos targetPosition, BlockPos referencePos,
        BlockPos templateRelativePos, StructureBlockInfo processedBlockInfo, StructurePlaceSettings settings)
    {
        if (level is null) return processedBlockInfo;
        //有真实关卡时工作用高度图换成对应对外的类型 测试高度图不进存档
        var heightmap = Heightmap;
        if (level.Level is not null)
        {
            if (heightmap == HeightmapTypes.WorldSurfaceWg) heightmap = HeightmapTypes.WorldSurface;
            else if (heightmap == HeightmapTypes.OceanFloorWg) heightmap = HeightmapTypes.OceanFloor;
        }
        var pos = processedBlockInfo.Pos;
        var height = level.GetHeight(heightmap, pos.X, pos.Z) + Offset;
        var delta = templateRelativePos.Y;
        return new StructureBlockInfo(new BlockPos(pos.X, height + delta, pos.Z), processedBlockInfo.State,
            processedBlockInfo.Nbt);
    }

    public MapCodec<StructureProcessor> ElementCodec =>
        new ProcessorMapCodec<GravityProcessor, StructureProcessor>(MapCodec);
}

//JigsawReplacementProcessor 把拼图方块换成它 nbt 里记录的最终状态 对应原版 JigsawReplacementProcessor
public sealed class JigsawReplacementProcessor : StructureProcessor
{
    public static readonly JigsawReplacementProcessor Instance = new();

    public static readonly MapCodec<JigsawReplacementProcessor> MapCodec =
        new StructureUnitMapCodec<JigsawReplacementProcessor>(() => Instance);

    private JigsawReplacementProcessor() { }

    public StructureBlockInfo? ProcessBlock(WorldGenRegion? level, BlockPos targetPosition, BlockPos referencePos,
        BlockPos templateRelativePos, StructureBlockInfo processedBlockInfo, StructurePlaceSettings settings)
    {
        if (!ProcessorBlockHelper.HasBlock(processedBlockInfo.State, "jigsaw")) return processedBlockInfo;
        //缺 nbt 的拼图方块不知道要换成什么 原版也是原样留下
        if (processedBlockInfo.Nbt is null) return processedBlockInfo;
        var stateString = processedBlockInfo.Nbt.GetString("final_state")?.Value ?? "minecraft:air";
        var parsed = ProcessorBlockHelper.ParseStateString(stateString);
        if (parsed is null) return null;
        if (ProcessorBlockHelper.HasBlock(parsed.Value, "structure_void")) return null;
        return new StructureBlockInfo(processedBlockInfo.Pos, parsed.Value, null);
    }

    public MapCodec<StructureProcessor> ElementCodec =>
        new ProcessorMapCodec<JigsawReplacementProcessor, StructureProcessor>(MapCodec);
}

//ProtectedBlockProcessor 保护世界里已有的一类方块不被覆盖 对应原版 ProtectedBlockProcessor
public sealed class ProtectedBlockProcessor : StructureProcessor
{
    public static readonly MapCodec<ProtectedBlockProcessor> MapCodec =
        new StructureSingleFieldMapCodec<ProtectedBlockProcessor, HolderSet<RegBlock>>(
            HolderSetCodecs.BlockSet.FieldOf("value"),
            set => new ProtectedBlockProcessor(set),
            p => p.CannotReplace);

    public HolderSet<RegBlock> CannotReplace { get; }

    public ProtectedBlockProcessor(HolderSet<RegBlock> cannotReplace) => CannotReplace = cannotReplace;

    public StructureBlockInfo? ProcessBlock(WorldGenRegion? level, BlockPos targetPosition, BlockPos referencePos,
        BlockPos templateRelativePos, StructureBlockInfo processedBlockInfo, StructurePlaceSettings settings)
    {
        if (level is null) return processedBlockInfo;
        var pos = processedBlockInfo.Pos;
        if (!ProcessorBlockHelper.InSet(level.GetBlockState(pos.X, pos.Y, pos.Z), CannotReplace))
            return processedBlockInfo;
        return null;
    }

    public MapCodec<StructureProcessor> ElementCodec =>
        new ProcessorMapCodec<ProtectedBlockProcessor, StructureProcessor>(MapCodec);
}

//BlackstoneReplaceProcessor 石头系列整体换成黑石系列 对应原版 BlackstoneReplaceProcessor
public sealed class BlackstoneReplaceProcessor : StructureProcessor
{
    public static readonly BlackstoneReplaceProcessor Instance = new();

    public static readonly MapCodec<BlackstoneReplaceProcessor> MapCodec =
        new StructureUnitMapCodec<BlackstoneReplaceProcessor>(() => Instance);

    private static readonly Dictionary<string, string> Replacements = new(StringComparer.Ordinal)
    {
        ["cobblestone"] = "blackstone",
        ["mossy_cobblestone"] = "blackstone",
        ["stone"] = "polished_blackstone",
        ["stone_bricks"] = "polished_blackstone_bricks",
        ["mossy_stone_bricks"] = "polished_blackstone_bricks",
        ["cobblestone_stairs"] = "blackstone_stairs",
        ["mossy_cobblestone_stairs"] = "blackstone_stairs",
        ["stone_stairs"] = "polished_blackstone_stairs",
        ["stone_brick_stairs"] = "polished_blackstone_brick_stairs",
        ["mossy_stone_brick_stairs"] = "polished_blackstone_brick_stairs",
        ["cobblestone_slab"] = "blackstone_slab",
        ["mossy_cobblestone_slab"] = "blackstone_slab",
        ["smooth_stone_slab"] = "polished_blackstone_slab",
        ["stone_slab"] = "polished_blackstone_slab",
        ["stone_brick_slab"] = "polished_blackstone_brick_slab",
        ["mossy_stone_brick_slab"] = "polished_blackstone_brick_slab",
        ["stone_brick_wall"] = "polished_blackstone_brick_wall",
        ["mossy_stone_brick_wall"] = "polished_blackstone_brick_wall",
        ["cobblestone_wall"] = "blackstone_wall",
        ["mossy_cobblestone_wall"] = "blackstone_wall",
        ["chiseled_stone_bricks"] = "chiseled_polished_blackstone",
        ["cracked_stone_bricks"] = "cracked_polished_blackstone_bricks",
        ["iron_bars"] = "iron_chain",
    };

    private BlackstoneReplaceProcessor() { }

    public StructureBlockInfo? ProcessBlock(WorldGenRegion? level, BlockPos targetPosition, BlockPos referencePos,
        BlockPos templateRelativePos, StructureBlockInfo processedBlockInfo, StructurePlaceSettings settings)
    {
        if (!Replacements.TryGetValue(processedBlockInfo.State.Owner.Id.Path, out var replacement)) return processedBlockInfo;
        var block = ProcessorBlockHelper.BlockOf(replacement);
        if (block is null) return processedBlockInfo;
        return new StructureBlockInfo(processedBlockInfo.Pos,
            CopyShapeProperties(processedBlockInfo.State, block.DefaultBlockState), processedBlockInfo.Nbt);
    }

    //CopyShapeProperties 只搬朝向 半砖位置 台阶类型三项 与原版逐属性比较一致
    private static BlockState CopyShapeProperties(BlockState from, BlockState to)
    {
        foreach (var entry in from.GetValues())
        {
            if (entry.Property.Name is not ("facing" or "half" or "type")) continue;
            var target = ProcessorBlockHelper.FindProperty(to, entry.Property.Name);
            if (target is null) continue;
            to = StructureBlockTransforms.SetIfAllowed(to, target, entry.Value);
        }
        return to;
    }

    public MapCodec<StructureProcessor> ElementCodec =>
        new ProcessorMapCodec<BlackstoneReplaceProcessor, StructureProcessor>(MapCodec);
}

//LavaSubmergedBlockProcessor 岩浆里的不完整方块换回岩浆 对应原版 LavaSubmergedBlockProcessor
public sealed class LavaSubmergedBlockProcessor : StructureProcessor
{
    public static readonly LavaSubmergedBlockProcessor Instance = new();

    public static readonly MapCodec<LavaSubmergedBlockProcessor> MapCodec =
        new StructureUnitMapCodec<LavaSubmergedBlockProcessor>(() => Instance);

    private LavaSubmergedBlockProcessor() { }

    public StructureBlockInfo? ProcessBlock(WorldGenRegion? level, BlockPos targetPosition, BlockPos referencePos,
        BlockPos templateRelativePos, StructureBlockInfo processedBlockInfo, StructurePlaceSettings settings)
    {
        if (level is null) return processedBlockInfo;
        var pos = processedBlockInfo.Pos;
        var wasLava = ProcessorBlockHelper.HasBlock(level.GetBlockState(pos.X, pos.Y, pos.Z), "lava");
        //原版按碰撞形状判是否整块 本作只有遮挡形状可用
        if (wasLava && !RegBlock.IsShapeFullBlock(processedBlockInfo.State.Owner.GetOcclusionShape(processedBlockInfo.State)))
            return new StructureBlockInfo(pos, ProcessorBlockHelper.StateOf("lava"), processedBlockInfo.Nbt);
        return processedBlockInfo;
    }

    public MapCodec<StructureProcessor> ElementCodec =>
        new ProcessorMapCodec<LavaSubmergedBlockProcessor, StructureProcessor>(MapCodec);
}
