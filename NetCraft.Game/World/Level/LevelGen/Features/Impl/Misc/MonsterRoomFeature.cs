using NetCraft.Codec;
using NetCraft.Game.World.Level.Block;
using NetCraft.Game.World.Level.LevelGen.Features.Impl.Vegetation;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Util.Random;
using RegBlock = NetCraft.Registry.Block;

namespace NetCraft.Game.World.Level.LevelGen.Features.Impl.Misc;

//MonsterRoomFeature dungeon room feature, maps to vanilla MonsterRoomFeature
//Carves a stone brick box the size of the room, places a chest in the only empty wall slot and a monster spawner at the center
public sealed class MonsterRoomFeature : Feature<NoneFeatureConfiguration>
{
    private const string FeatureId = "monster_room";

    public static readonly MonsterRoomFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new MonsterRoomFeature());

    //FeaturesCannotReplaceTag blocks features cannot replace, maps to vanilla BlockTags.FEATURES_CANNOT_REPLACE
    private static readonly TagKey<RegBlock> FeaturesCannotReplaceTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("features_cannot_replace"));

    //MobCount number of selectable mobs for the spawner, maps to the length of the vanilla MOBS array
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
                //Vanilla then attaches the dungeon loot table to the chest; NetCraft has no chunk block entity system yet, so only the block is placed
                break;
            }
        }
        SafeSetBlock(level, origin, VegetationSupport.StateOf("spawner"));
        //Vanilla fetches the spawner block entity and picks a random mob; block entities are not wired up here, so this only consumes that random value to keep the sequence aligned
        random.NextInt(MobCount);
        return true;
    }

    //Reorient orient the chest by its solid neighbors, maps to vanilla StructurePiece.reorient
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
        //The chest faces north by default, matching the default state passed in
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

    //SafeSetBlock write only when the target cell is not in the cannot-replace tag, maps to vanilla safeSetBlock
    private static void SafeSetBlock(WorldGenRegion level, BlockPos pos, BlockState state)
    {
        if (VegetationSupport.InTag(Get(level, pos), FeaturesCannotReplaceTag)) return;
        Set(level, pos, state);
    }

    //IsSolidRender whether the state's occlusion shape fills the whole cell, maps to vanilla isSolidRender
    private static bool IsSolidRender(WorldGenRegion level, BlockPos pos)
    {
        var state = Get(level, pos);
        return state.Owner.SolidRender(state);
    }

    //IsSolid whether the state counts as solid, maps to vanilla isSolid
    //NetCraft has no material system, so approximate with "has collision, is not air and carries no fluid"
    private static bool IsSolid(BlockState state)
        => !state.Owner.IsAir && state.FluidState.IsEmpty
            && state.Owner is BlockBehaviour behaviour && behaviour.HasCollision;

    private static bool IsAir(WorldGenRegion level, BlockPos pos) => Get(level, pos).Owner.IsAir;

    private static BlockState Get(WorldGenRegion level, BlockPos pos)
        => level.GetBlockState(pos.X, pos.Y, pos.Z);

    private static void Set(WorldGenRegion level, BlockPos pos, BlockState state)
        => level.SetBlockState(pos.X, pos.Y, pos.Z, state);
}

//MiscBootstrap misc feature registration entry
//Touching each static Instance triggers static registration
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
