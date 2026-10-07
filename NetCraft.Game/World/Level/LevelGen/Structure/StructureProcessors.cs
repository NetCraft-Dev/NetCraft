using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using RegBlock = NetCraft.Registry.Block;
using HeightmapTypes = NetCraft.Registry.Heightmap.Types;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//NopProcessor no-op processor, maps to vanilla NopProcessor; changes nothing
public sealed class NopProcessor : StructureProcessor
{
    public static readonly NopProcessor Instance = new();

    public static readonly MapCodec<NopProcessor> MapCodec = new StructureUnitMapCodec<NopProcessor>(() => Instance);

    private NopProcessor() { }

    public MapCodec<StructureProcessor> ElementCodec =>
        new ProcessorMapCodec<NopProcessor, StructureProcessor>(MapCodec);
}

//BlockIgnoreProcessor ignores the given blocks, maps to vanilla BlockIgnoreProcessor; a hit cell is dropped outright
public sealed class BlockIgnoreProcessor : StructureProcessor
{
    //In vanilla the blocks field is a block state list; here only the block itself is taken
    public static readonly MapCodec<BlockIgnoreProcessor> MapCodec =
        new StructureSingleFieldMapCodec<BlockIgnoreProcessor, IReadOnlyList<BlockState>>(
            BlockStateCodec.Instance.ListOf().FieldOf("blocks"),
            states => new BlockIgnoreProcessor(states.Select(s => s.Owner).ToList()),
            p => p.ToIgnore.Select(b => b.DefaultBlockState).ToList());

    private static BlockIgnoreProcessor? _structureBlock;
    private static BlockIgnoreProcessor? _air;
    private static BlockIgnoreProcessor? _structureAndAir;

    //StructureBlock ignores only structure blocks, maps to vanilla STRUCTURE_BLOCK
    public static BlockIgnoreProcessor StructureBlock
        => _structureBlock ??= new BlockIgnoreProcessor(new[] { ProcessorBlockHelper.BlockOf("structure_block")! });

    //Air ignores only air, maps to vanilla AIR
    public static BlockIgnoreProcessor Air
        => _air ??= new BlockIgnoreProcessor(new[] { ProcessorBlockHelper.BlockOf("air")! });

    //StructureAndAir ignores air and structure blocks, maps to vanilla STRUCTURE_AND_AIR
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

//BlockRotProcessor randomly drops blocks by integrity, maps to vanilla BlockRotProcessor
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

//GravityProcessor snaps blocks to the heightmap, maps to vanilla GravityProcessor
//Height cannot be resolved without a world view; vanilla always has a level on this path, and the block is kept as is here
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
        //With a real level, the working heightmap switches to the corresponding public type; the test heightmap is not saved
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

//JigsawReplacementProcessor replaces a jigsaw block with the final state recorded in its nbt, maps to vanilla JigsawReplacementProcessor
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
        //A jigsaw block without nbt has no known final state; vanilla also leaves it as is
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

//ProtectedBlockProcessor protects an existing block category in the world from being overwritten, maps to vanilla ProtectedBlockProcessor
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

//BlackstoneReplaceProcessor swaps the whole stone family for blackstone, maps to vanilla BlackstoneReplaceProcessor
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

    //CopyShapeProperties copies only the three properties facing / half / type, matching vanilla's per-property comparison
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

//LavaSubmergedBlockProcessor turns incomplete blocks submerged in lava back into lava, maps to vanilla LavaSubmergedBlockProcessor
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
        //Vanilla checks the collision shape for full-block; here only the occlusion shape is available
        if (wasLava && !RegBlock.IsShapeFullBlock(processedBlockInfo.State.Owner.GetOcclusionShape(processedBlockInfo.State)))
            return new StructureBlockInfo(pos, ProcessorBlockHelper.StateOf("lava"), processedBlockInfo.Nbt);
        return processedBlockInfo;
    }

    public MapCodec<StructureProcessor> ElementCodec =>
        new ProcessorMapCodec<LavaSubmergedBlockProcessor, StructureProcessor>(MapCodec);
}
