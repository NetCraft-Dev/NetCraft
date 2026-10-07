using NetCraft.Codec;
using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using RegBlock = NetCraft.Registry.Block;

namespace NetCraft.Game.World.Level.LevelGen.Features.Impl.Nether;

//NetherSupport shared block access, position traversal and height tools for nether and end features
internal static class NetherSupport
{
    //NyliumTag nylium tag, maps to vanilla BlockTags.NYLIUM
    public static readonly TagKey<RegBlock> NyliumTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("nylium"));

    //SupportsChorusPlantTag tag of blocks that support chorus plants, maps to vanilla BlockTags.SUPPORTS_CHORUS_PLANT
    public static readonly TagKey<RegBlock> SupportsChorusPlantTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("supports_chorus_plant"));

    //SupportsChorusFlowerTag tag of blocks that support chorus flowers, maps to vanilla BlockTags.SUPPORTS_CHORUS_FLOWER
    public static readonly TagKey<RegBlock> SupportsChorusFlowerTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("supports_chorus_flower"));

    //State default block state by registry name; falls back to air when the block is missing
    public static BlockState State(string path)
        => BuiltInRegistries.BLOCK.GetValue(Identifier.WithDefaultNamespace(path))?.DefaultBlockState
            ?? Blocks.AIR.DefaultBlockState;

    //Block fetch a block by registry name; falls back to air when unregistered
    public static RegBlock Block(string path)
        => BuiltInRegistries.BLOCK.GetValue(Identifier.WithDefaultNamespace(path)) ?? Blocks.AIR;

    //GetBlockState fetch the block state at the position
    public static BlockState GetBlockState(WorldGenRegion level, BlockPos pos)
        => level.GetBlockState(pos.X, pos.Y, pos.Z);

    //IsAir whether the position is air, maps to vanilla isEmptyBlock
    public static bool IsAir(WorldGenRegion level, BlockPos pos) => GetBlockState(level, pos).Owner.IsAir;

    //IsBlock whether the position is the given block, maps to vanilla BlockState.is(Block)
    public static bool IsBlock(WorldGenRegion level, BlockPos pos, RegBlock block)
        => GetBlockState(level, pos).Owner == block;

    //MatchesTag whether the block at the position is in the tag; treat an unloaded tag as no match
    public static bool MatchesTag(WorldGenRegion level, BlockPos pos, TagKey<RegBlock> tag)
        => StateMatchesTag(GetBlockState(level, pos), tag);

    //StateMatchesTag whether the state's block is in the tag; treat an unloaded tag as no match
    public static bool StateMatchesTag(BlockState state, TagKey<RegBlock> tag)
    {
        var set = BuiltInRegistries.BLOCK.Get(tag);
        return set is not null && set.IsBound
            && set.Contains(BuiltInRegistries.BLOCK.WrapAsHolder(state.Owner));
    }

    //CanBeReplaced whether the state can be replaced, maps to vanilla BlockState.canBeReplaced
    public static bool CanBeReplaced(BlockState state)
        => state.Owner is BlockBehaviour behaviour && behaviour.CanBeReplaced;

    //CanSurvive whether the state can stay in place, maps to vanilla canSurvive; treated as surviving while no server world is wired up
    public static bool CanSurvive(WorldGenRegion level, BlockPos pos, BlockState state)
        => level.Level is not { } server
            || state.Owner is not BlockBehaviour behaviour
            || behaviour.CanSurvive(server, pos, state);

    //MinY lowest Y of the generation range, maps to vanilla getMinY
    public static int MinY(WorldGenRegion level) => level.MinSectionY * 16;

    //MaxBuildHeight exclusive upper bound of the generation range, maps to vanilla getMaxBuildHeight
    public static int MaxBuildHeight(WorldGenRegion level) => (level.MaxSectionY + 1) * 16;

    //MaxY highest Y of the generation range, inclusive, maps to vanilla getMaxY
    public static int MaxY(WorldGenRegion level) => MaxBuildHeight(level) - 1;

    //IsOutsideBuildHeight whether the Y is outside the generation range, maps to vanilla isOutsideBuildHeight
    public static bool IsOutsideBuildHeight(WorldGenRegion level, int y)
        => y < MinY(level) || y >= MaxBuildHeight(level);

    //SetBlock place a block, maps to vanilla Feature.setBlock
    public static void SetBlock(WorldGenRegion level, BlockPos pos, BlockState state)
        => level.SetBlockState(pos.X, pos.Y, pos.Z, state);

    //DistManhattan Manhattan distance between two points, maps to vanilla BlockPos.distManhattan
    public static int DistManhattan(BlockPos a, BlockPos b)
        => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z);

    //ToOptional convert a nullable block position to Optional, maps to vanilla Optional.ofNullable
    public static Optional<BlockPos> ToOptional(BlockPos? pos)
        => pos.HasValue ? Optional<BlockPos>.Of(pos.Value) : Optional<BlockPos>.Empty();

    //BetweenClosed iteration order over a closed bounding box: z outer, y middle, x inner. maps to vanilla BlockPos.betweenClosed
    public static IEnumerable<BlockPos> BetweenClosed(int minX, int minY, int minZ, int maxX, int maxY, int maxZ)
    {
        for (var z = minZ; z <= maxZ; z++)
        for (var y = minY; y <= maxY; y++)
        for (var x = minX; x <= maxX; x++)
            yield return new BlockPos(x, y, z);
    }

    //WithinManhattan layer-by-layer traversal inside a Manhattan ball, maps to vanilla BlockPos.withinManhattan
    //Layer and mirror order are copied from vanilla; a different order changes terrain for the same seed
    public static IEnumerable<BlockPos> WithinManhattan(BlockPos origin, int reachX, int reachY, int reachZ)
    {
        var maxDepth = reachX + reachY + reachZ;
        var originX = origin.X;
        var originY = origin.Y;
        var originZ = origin.Z;
        var currentDepth = 0;
        var maxX = 0;
        var maxY = 0;
        var x = 0;
        var y = 0;
        var zMirror = false;
        var currentZ = 0;
        while (true)
        {
            if (zMirror)
            {
                zMirror = false;
                yield return new BlockPos(originX + x, originY + y, originZ - currentZ);
                continue;
            }
            BlockPos? found = null;
            while (found is null)
            {
                if (y > maxY)
                {
                    x++;
                    if (x > maxX)
                    {
                        currentDepth++;
                        if (currentDepth > maxDepth) yield break;
                        maxX = Math.Min(reachX, currentDepth);
                        x = -maxX;
                    }
                    maxY = Math.Min(reachY, currentDepth - Math.Abs(x));
                    y = -maxY;
                }
                var zz = currentDepth - Math.Abs(x) - Math.Abs(y);
                if (zz <= reachZ)
                {
                    zMirror = zz != 0;
                    currentZ = zz;
                    found = new BlockPos(originX + x, originY + y, originZ + zz);
                }
                y++;
            }
            yield return found.Value;
        }
    }
}
