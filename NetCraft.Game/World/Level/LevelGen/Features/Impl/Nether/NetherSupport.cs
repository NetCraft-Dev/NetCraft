using NetCraft.Codec;
using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using RegBlock = NetCraft.Registry.Block;

namespace NetCraft.Game.World.Level.LevelGen.Features.Impl.Nether;

//NetherSupport 下界与末地特征共用的方块获取 位置遍历与高度工具
internal static class NetherSupport
{
    //NyliumTag 菌岩标签 对应原版 BlockTags.NYLIUM
    public static readonly TagKey<RegBlock> NyliumTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("nylium"));

    //SupportsChorusPlantTag 可支撑紫颂植株的方块标签 对应原版 BlockTags.SUPPORTS_CHORUS_PLANT
    public static readonly TagKey<RegBlock> SupportsChorusPlantTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("supports_chorus_plant"));

    //SupportsChorusFlowerTag 可支撑紫颂花的方块标签 对应原版 BlockTags.SUPPORTS_CHORUS_FLOWER
    public static readonly TagKey<RegBlock> SupportsChorusFlowerTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("supports_chorus_flower"));

    //State 按注册名取方块默认状态 方块表里没有该方块时退回空气
    public static BlockState State(string path)
        => BuiltInRegistries.BLOCK.GetValue(Identifier.WithDefaultNamespace(path))?.DefaultBlockState
            ?? Blocks.AIR.DefaultBlockState;

    //Block 按注册名取方块 未注册时退回空气
    public static RegBlock Block(string path)
        => BuiltInRegistries.BLOCK.GetValue(Identifier.WithDefaultNamespace(path)) ?? Blocks.AIR;

    //GetBlockState 按位置取方块状态
    public static BlockState GetBlockState(WorldGenRegion level, BlockPos pos)
        => level.GetBlockState(pos.X, pos.Y, pos.Z);

    //IsAir 该位置是否为空 对应原版 isEmptyBlock
    public static bool IsAir(WorldGenRegion level, BlockPos pos) => GetBlockState(level, pos).Owner.IsAir;

    //IsBlock 该位置是否为指定方块 对应原版 BlockState.is(Block)
    public static bool IsBlock(WorldGenRegion level, BlockPos pos, RegBlock block)
        => GetBlockState(level, pos).Owner == block;

    //MatchesTag 该位置方块是否属于标签 标签未装载时按不匹配处理
    public static bool MatchesTag(WorldGenRegion level, BlockPos pos, TagKey<RegBlock> tag)
        => StateMatchesTag(GetBlockState(level, pos), tag);

    //StateMatchesTag 该状态所属方块是否属于标签 标签未装载时按不匹配处理
    public static bool StateMatchesTag(BlockState state, TagKey<RegBlock> tag)
    {
        var set = BuiltInRegistries.BLOCK.Get(tag);
        return set is not null && set.IsBound
            && set.Contains(BuiltInRegistries.BLOCK.WrapAsHolder(state.Owner));
    }

    //CanBeReplaced 该状态能否被替换 对应原版 BlockState.canBeReplaced
    public static bool CanBeReplaced(BlockState state)
        => state.Owner is BlockBehaviour behaviour && behaviour.CanBeReplaced;

    //CanSurvive 该状态能否留在原位 对应原版 canSurvive 未接入服务端世界时按可存活处理
    public static bool CanSurvive(WorldGenRegion level, BlockPos pos, BlockState state)
        => level.Level is not { } server
            || state.Owner is not BlockBehaviour behaviour
            || behaviour.CanSurvive(server, pos, state);

    //MinY 可生成范围最低 Y 对应原版 getMinY
    public static int MinY(WorldGenRegion level) => level.MinSectionY * 16;

    //MaxBuildHeight 可生成范围的开区间上界 对应原版 getMaxBuildHeight
    public static int MaxBuildHeight(WorldGenRegion level) => (level.MaxSectionY + 1) * 16;

    //MaxY 可生成范围最高 Y 包含式 对应原版 getMaxY
    public static int MaxY(WorldGenRegion level) => MaxBuildHeight(level) - 1;

    //IsOutsideBuildHeight 该 Y 是否越出可生成范围 对应原版 isOutsideBuildHeight
    public static bool IsOutsideBuildHeight(WorldGenRegion level, int y)
        => y < MinY(level) || y >= MaxBuildHeight(level);

    //SetBlock 放置方块 对应原版 Feature.setBlock
    public static void SetBlock(WorldGenRegion level, BlockPos pos, BlockState state)
        => level.SetBlockState(pos.X, pos.Y, pos.Z, state);

    //DistManhattan 两点的曼哈顿距离 对应原版 BlockPos.distManhattan
    public static int DistManhattan(BlockPos a, BlockPos b)
        => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z);

    //ToOptional 可空方块位置转 Optional 对应原版 Optional.ofNullable
    public static Optional<BlockPos> ToOptional(BlockPos? pos)
        => pos.HasValue ? Optional<BlockPos>.Of(pos.Value) : Optional<BlockPos>.Empty();

    //BetweenClosed 闭区间长方体的遍历顺序 z 最外 y 居中 x 最内 对应原版 BlockPos.betweenClosed
    public static IEnumerable<BlockPos> BetweenClosed(int minX, int minY, int minZ, int maxX, int maxY, int maxZ)
    {
        for (var z = minZ; z <= maxZ; z++)
        for (var y = minY; y <= maxY; y++)
        for (var x = minX; x <= maxX; x++)
            yield return new BlockPos(x, y, z);
    }

    //WithinManhattan 曼哈顿球内按层展开的遍历 对应原版 BlockPos.withinManhattan
    //层级与镜像补充都照抄原版 顺序变了同种子长出的地形会不同
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
