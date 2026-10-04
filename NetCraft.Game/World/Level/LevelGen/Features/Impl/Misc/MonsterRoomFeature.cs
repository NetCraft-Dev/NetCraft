using NetCraft.Codec;
using NetCraft.Game.World.Level.Block;
using NetCraft.Game.World.Level.LevelGen.Features.Impl.Vegetation;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Util.Random;
using RegBlock = NetCraft.Registry.Block;

namespace NetCraft.Game.World.Level.LevelGen.Features.Impl.Misc;

//MonsterRoomFeature 地牢房间特征 对应原版 MonsterRoomFeature
//按房间规模挖出一个石砖盒子 在唯一一面靠墙的空位放箱子 正中间放刷怪笼
public sealed class MonsterRoomFeature : Feature<NoneFeatureConfiguration>
{
    private const string FeatureId = "monster_room";

    public static readonly MonsterRoomFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new MonsterRoomFeature());

    //FeaturesCannotReplaceTag 特征不可替换的方块 对应原版 BlockTags.FEATURES_CANNOT_REPLACE
    private static readonly TagKey<RegBlock> FeaturesCannotReplaceTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("features_cannot_replace"));

    //MobCount 刷怪笼可选的生物数 对应原版 MOBS 数组长度
    private const int MobCount = 4;

    private MonsterRoomFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), NoneFeatureConfiguration.Codec) { }

    protected override bool Place(NoneFeatureConfiguration config, FeaturePlaceContext context)
    {
        var origin = context.Origin;
        var random = context.Random;
        var level = context.Level;
        var minY = level.MinSectionY * 16;
        var xr = random.NextInt(2) + 2;
        var minX = -xr - 1;
        var maxX = xr + 1;
        var zr = random.NextInt(2) + 2;
        var minZ = -zr - 1;
        var maxZ = zr + 1;
        var holeCount = 0;
        for (var dx = minX; dx <= maxX; dx++)
        {
            for (var dy = -1; dy <= 4; dy++)
            {
                for (var dz = minZ; dz <= maxZ; dz++)
                {
                    var holePos = origin.Offset(dx, dy, dz);
                    var solid = IsSolid(Get(level, holePos));
                    if (dy == -1 && !solid) return false;
                    if (dy == 4 && !solid) return false;
                    if ((dx == minX || dx == maxX || dz == minZ || dz == maxZ) && dy == 0
                        && IsAir(level, holePos) && IsAir(level, holePos.Offset(0, 1, 0))) holeCount++;
                }
            }
        }
        if (holeCount < 1 || holeCount > 5) return false;
        for (var dx = minX; dx <= maxX; dx++)
        {
            for (var dy = 3; dy >= -1; dy--)
            {
                for (var dz = minZ; dz <= maxZ; dz++)
                {
                    var wallBlock = origin.Offset(dx, dy, dz);
                    var wallState = Get(level, wallBlock);
                    if (dx == minX || dy == -1 || dz == minZ || dx == maxX || dy == 4 || dz == maxZ)
                    {
                        if (wallBlock.Y >= minY && !IsSolid(Get(level, wallBlock.Offset(0, -1, 0))))
                        {
                            Set(level, wallBlock, VegetationSupport.StateOf("cave_air"));
                            continue;
                        }
                        if (!IsSolid(wallState) || VegetationSupport.IsState(wallState, "chest")) continue;
                        if (dy == -1 && random.NextInt(4) != 0)
                        {
                            SafeSetBlock(level, wallBlock, VegetationSupport.StateOf("mossy_cobblestone"));
                            continue;
                        }
                        SafeSetBlock(level, wallBlock, VegetationSupport.StateOf("cobblestone"));
                        continue;
                    }
                    if (VegetationSupport.IsState(wallState, "chest")
                        || VegetationSupport.IsState(wallState, "spawner")) continue;
                    SafeSetBlock(level, wallBlock, VegetationSupport.StateOf("cave_air"));
                }
            }
        }
        for (var chestTry = 0; chestTry < 2; chestTry++)
        {
            for (var i = 0; i < 3; i++)
            {
                var xc = origin.X + random.NextInt(xr * 2 + 1) - xr;
                var zc = origin.Z + random.NextInt(zr * 2 + 1) - zr;
                var chestPos = new BlockPos(xc, origin.Y, zc);
                if (!IsAir(level, chestPos)) continue;
                var wallCount = 0;
                foreach (var direction in VegetationSupport.HorizontalPlane)
                {
                    if (!IsSolid(Get(level, chestPos.Offset(direction)))) continue;
                    wallCount++;
                }
                if (wallCount != 1) continue;
                SafeSetBlock(level, chestPos, Reorient(level, chestPos, VegetationSupport.StateOf("chest")));
                //原版接着给箱子挂地牢战利品表 本作区块方块实体体系未接 只放方块
                break;
            }
        }
        SafeSetBlock(level, origin, VegetationSupport.StateOf("spawner"));
        //原版取出刷怪笼方块实体并随机挑一种生物 本作方块实体未接 这里只消耗那次随机数保持序列一致
        random.NextInt(MobCount);
        return true;
    }

    //Reorient 按周围实心邻居定箱子的朝向 对应原版 StructurePiece.reorient
    private static BlockState Reorient(WorldGenRegion level, BlockPos pos, BlockState state)
    {
        Direction? solidNeighbour = null;
        foreach (var direction in VegetationSupport.HorizontalPlane)
        {
            var neighbourState = Get(level, pos.Offset(direction));
            if (VegetationSupport.IsState(neighbourState, "chest")) return state;
            if (!neighbourState.Owner.SolidRender(neighbourState)) continue;
            if (solidNeighbour is null)
            {
                solidNeighbour = direction;
            }
            else
            {
                solidNeighbour = null;
                break;
            }
        }
        if (solidNeighbour is { } lockedNeighbour)
            return VegetationSupport.WithProperty(state, "facing", VegetationSupport.FaceName(lockedNeighbour.Opposite));
        //箱子默认朝向是北 与传入的默认状态一致
        var lockDir = Direction.North;
        var relativePos = pos.Offset(lockDir);
        if (IsSolidRender(level, relativePos))
        {
            lockDir = lockDir.Opposite;
            relativePos = pos.Offset(lockDir);
        }
        if (IsSolidRender(level, relativePos))
        {
            lockDir = lockDir.ClockWise;
            relativePos = pos.Offset(lockDir);
        }
        if (IsSolidRender(level, relativePos)) lockDir = lockDir.Opposite;
        return VegetationSupport.WithProperty(state, "facing", VegetationSupport.FaceName(lockDir));
    }

    //SafeSetBlock 目标格不在禁改标签里才写入 对应原版 safeSetBlock
    private static void SafeSetBlock(WorldGenRegion level, BlockPos pos, BlockState state)
    {
        if (VegetationSupport.InTag(Get(level, pos), FeaturesCannotReplaceTag)) return;
        Set(level, pos, state);
    }

    //IsSolidRender 该状态的遮挡形状占满整格 对应原版 isSolidRender
    private static bool IsSolidRender(WorldGenRegion level, BlockPos pos)
    {
        var state = Get(level, pos);
        return state.Owner.SolidRender(state);
    }

    //IsSolid 该状态是否算实心 对应原版 isSolid
    //本作没有材质体系 这里用「有碰撞且不是空气且不带流体」近似
    private static bool IsSolid(BlockState state)
        => !state.Owner.IsAir && state.FluidState.IsEmpty
            && state.Owner is BlockBehaviour behaviour && behaviour.HasCollision;

    private static bool IsAir(WorldGenRegion level, BlockPos pos) => Get(level, pos).Owner.IsAir;

    private static BlockState Get(WorldGenRegion level, BlockPos pos)
        => level.GetBlockState(pos.X, pos.Y, pos.Z);

    private static void Set(WorldGenRegion level, BlockPos pos, BlockState state)
        => level.SetBlockState(pos.X, pos.Y, pos.Z, state);
}

//MiscBootstrap 杂项特征注册入口
//触碰各静态 Instance 使静态注册生效
public static class MiscBootstrap
{
    public static void RegisterAll()
    {
        _ = GeodeFeature.Instance;
        _ = FossilFeature.Instance;
        _ = SculkPatchFeature.Instance;
        _ = DesertWellFeature.Instance;
        _ = LakeFeature.Instance;
        _ = MonsterRoomFeature.Instance;
        _ = SpeleothemFeature.Instance;
        _ = CoralTreeFeature.Instance;
        _ = CoralClawFeature.Instance;
        _ = CoralMushroomFeature.Instance;
        _ = TemplateFeature.Instance;
    }
}
