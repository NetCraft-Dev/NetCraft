using NetCraft.Codec;
using NetCraft.Game.World.Level.Block;
using NetCraft.Game.World.Level.LevelGen.BlockPredicates;
using NetCraft.Game.World.Level.LevelGen.Features.Impl.Vegetation;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Features.Impl.Misc;

//DesertWellFeature desert well feature, maps to vanilla DesertWellFeature
//Finds the first non-air block below the origin, checks it is sand, then lays three sandstone layers and the well outline, and finally buries two suspicious sand blocks
public sealed class DesertWellFeature : Feature<NoneFeatureConfiguration>
{
    private const string FeatureId = "desert_well";

    public static readonly DesertWellFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new DesertWellFeature());

    private DesertWellFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), NoneFeatureConfiguration.Codec) { }

    protected override bool Place(NoneFeatureConfiguration config, FeaturePlaceContext context)
    {
        var level = context.Level;
        var minY = level.MinSectionY * 16;
        var origin = context.Origin.Offset(0, 1, 0);
        while (VegetationSupport.Get(level, origin).Owner.IsAir && origin.Y > minY + 2)
            origin = origin.Offset(0, -1, 0);
        if (!VegetationSupport.IsState(VegetationSupport.Get(level, origin), "sand")) return false;
        for (var ox = -2; ox <= 2; ox++)
        {
            for (var oz = -2; oz <= 2; oz++)
            {
                if (IsAir(level, origin.Offset(ox, -1, oz)) && IsAir(level, origin.Offset(ox, -2, oz)))
                    return false;
            }
        }
        var sandstone = VegetationSupport.StateOf("sandstone");
        var sand = VegetationSupport.StateOf("sand");
        var sandstoneSlab = VegetationSupport.StateOf("sandstone_slab");
        var water = VegetationSupport.StateOf("water");
        for (var oy = -2; oy <= 0; oy++)
        {
            for (var ox = -2; ox <= 2; ox++)
            {
                for (var oz = -2; oz <= 2; oz++)
                    Set(level, origin.Offset(ox, oy, oz), sandstone);
            }
        }
        Set(level, origin, water);
        foreach (var direction in VegetationSupport.HorizontalPlane)
            Set(level, origin.Offset(direction), water);
        var sandCenter = origin.Offset(0, -1, 0);
        Set(level, sandCenter, sand);
        foreach (var direction in VegetationSupport.HorizontalPlane)
            Set(level, sandCenter.Offset(direction), sand);
        for (var ox = -2; ox <= 2; ox++)
        {
            for (var oz = -2; oz <= 2; oz++)
            {
                if (ox == -2 || ox == 2 || oz == -2 || oz == 2)
                    Set(level, origin.Offset(ox, 1, oz), sandstone);
            }
        }
        Set(level, origin.Offset(2, 1, 0), sandstoneSlab);
        Set(level, origin.Offset(-2, 1, 0), sandstoneSlab);
        Set(level, origin.Offset(0, 1, 2), sandstoneSlab);
        Set(level, origin.Offset(0, 1, -2), sandstoneSlab);
        for (var ox = -1; ox <= 1; ox++)
        {
            for (var oz = -1; oz <= 1; oz++)
            {
                Set(level, origin.Offset(ox, 4, oz),
                    ox == 0 && oz == 0 ? sandstone : sandstoneSlab);
            }
        }
        for (var oy = 1; oy <= 3; oy++)
        {
            Set(level, origin.Offset(-1, oy, -1), sandstone);
            Set(level, origin.Offset(-1, oy, 1), sandstone);
            Set(level, origin.Offset(1, oy, -1), sandstone);
            Set(level, origin.Offset(1, oy, 1), sandstone);
        }
        var waterPositions = new[]
        {
            origin, origin.Offset(1, 0, 0), origin.Offset(0, 0, 1),
            origin.Offset(-1, 0, 0), origin.Offset(0, 0, -1),
        };
        var random = context.Random;
        PlaceSuspiciousSand(level, waterPositions[random.NextInt(waterPositions.Length)].Offset(0, -1, 0));
        PlaceSuspiciousSand(level, waterPositions[random.NextInt(waterPositions.Length)].Offset(0, -2, 0));
        return true;
    }

    //PlaceSuspiciousSand bury one suspicious sand block, maps to vanilla placeSusSand
    //Vanilla also attaches the archaeology loot table to the block entity; NetCraft has no chunk block entity system yet, so only the block is placed
    private static void PlaceSuspiciousSand(WorldGenRegion level, BlockPos pos)
        => Set(level, pos, VegetationSupport.StateOf("suspicious_sand"));

    private static bool IsAir(WorldGenRegion level, BlockPos pos) => VegetationSupport.Get(level, pos).Owner.IsAir;

    private static void Set(WorldGenRegion level, BlockPos pos, BlockState state) => VegetationSupport.Set(level, pos, state);
}

//LakeConfiguration lake configuration, maps to vanilla LakeFeature.Configuration
//The fluid, barrier and three replaceable predicates all come from the config
public sealed class LakeConfiguration : FeatureConfiguration
{
    public static readonly Codec<LakeConfiguration> Codec =
        RecordCodecBuilder.Of5<LakeConfiguration, BlockStateProvider, BlockStateProvider, BlockPredicate,
            BlockPredicate, BlockPredicate>(
            BlockStateProvider.Codec.FieldOf("fluid")
                .ForGetter<LakeConfiguration, BlockStateProvider>(c => c.Fluid),
            BlockStateProvider.Codec.FieldOf("barrier")
                .ForGetter<LakeConfiguration, BlockStateProvider>(c => c.Barrier),
            BlockPredicate.Codec.FieldOf("can_place_feature")
                .ForGetter<LakeConfiguration, BlockPredicate>(c => c.CanPlaceFeature),
            BlockPredicate.Codec.FieldOf("can_replace_with_air_or_fluid")
                .ForGetter<LakeConfiguration, BlockPredicate>(c => c.CanReplaceWithAirOrFluid),
            BlockPredicate.Codec.FieldOf("can_replace_with_barrier")
                .ForGetter<LakeConfiguration, BlockPredicate>(c => c.CanReplaceWithBarrier),
            (fluid, barrier, canPlaceFeature, canReplaceWithAirOrFluid, canReplaceWithBarrier) =>
                new LakeConfiguration(fluid, barrier, canPlaceFeature, canReplaceWithAirOrFluid,
                    canReplaceWithBarrier));

    public BlockStateProvider Fluid { get; }
    public BlockStateProvider Barrier { get; }
    public BlockPredicate CanPlaceFeature { get; }
    public BlockPredicate CanReplaceWithAirOrFluid { get; }
    public BlockPredicate CanReplaceWithBarrier { get; }

    public LakeConfiguration(BlockStateProvider fluid, BlockStateProvider barrier, BlockPredicate canPlaceFeature,
        BlockPredicate canReplaceWithAirOrFluid, BlockPredicate canReplaceWithBarrier)
    {
        Fluid = fluid;
        Barrier = barrier;
        CanPlaceFeature = canPlaceFeature;
        CanReplaceWithAirOrFluid = canReplaceWithAirOrFluid;
        CanReplaceWithBarrier = canReplaceWithBarrier;
    }
}

//LakeFeature lake feature, maps to vanilla LakeFeature
//First randomly kneads a few ellipsoids into a 16x8x16 3D grid, then decides per cell and neighbor whether to fill fluid or carve air
public sealed class LakeFeature : Feature<LakeConfiguration>
{
    private const string FeatureId = "lake";

    public static readonly LakeFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new LakeFeature());

    private LakeFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), LakeConfiguration.Codec) { }

    protected override bool Place(LakeConfiguration config, FeaturePlaceContext context)
    {
        var origin = context.Origin;
        var level = context.Level;
        var random = context.Random;
        if (origin.Y <= level.MinSectionY * 16 + 4) return false;
        var origin2 = origin.Offset(-8, -4, -8);
        var grid = new bool[2048];
        var spots = random.NextInt(4) + 4;
        for (var i = 0; i < spots; i++)
        {
            var xr = random.NextDouble() * 6.0d + 3.0d;
            var yr = random.NextDouble() * 4.0d + 2.0d;
            var zr = random.NextDouble() * 6.0d + 3.0d;
            var xp = random.NextDouble() * (16.0d - xr - 2.0d) + 1.0d + xr / 2.0d;
            var yp = random.NextDouble() * (8.0d - yr - 4.0d) + 2.0d + yr / 2.0d;
            var zp = random.NextDouble() * (16.0d - zr - 2.0d) + 1.0d + zr / 2.0d;
            for (var xx = 1; xx < 15; xx++)
            {
                for (var zz = 1; zz < 15; zz++)
                {
                    for (var yy = 1; yy < 7; yy++)
                    {
                        var xd = (xx - xp) / (xr / 2.0d);
                        var yd = (yy - yp) / (yr / 2.0d);
                        var zd = (zz - zp) / (zr / 2.0d);
                        if (xd * xd + yd * yd + zd * zd < 1.0d) grid[GridIndex(xx, zz, yy)] = true;
                    }
                }
            }
        }
        var fluid = config.Fluid.GetState(level, random, origin2);
        for (var xx = 0; xx < 16; xx++)
        {
            for (var zz = 0; zz < 16; zz++)
            {
                for (var yy = 0; yy < 8; yy++)
                {
                    if (!IsShell(xx, zz, yy, grid)) continue;
                    var offsetPos = origin2.Offset(xx, yy, zz);
                    var blockState = VegetationSupport.Get(level, offsetPos);
                    if (yy >= 4 && !blockState.FluidState.IsEmpty) return false;
                    if ((yy < 4 && !IsSolid(blockState) && blockState != fluid)
                        || !config.CanPlaceFeature.Test(level, offsetPos)) return false;
                }
            }
        }
        var air = VegetationSupport.StateOf("cave_air");
        for (var xx = 0; xx < 16; xx++)
        {
            for (var zz = 0; zz < 16; zz++)
            {
                for (var yy = 0; yy < 8; yy++)
                {
                    if (!grid[GridIndex(xx, zz, yy)]) continue;
                    var placePos = origin2.Offset(xx, yy, zz);
                    if (!config.CanReplaceWithAirOrFluid.Test(level, placePos)) continue;
                    var placeAir = yy >= 4;
                    //Vanilla schedules a tick and registers post-processing when placing air below; there is no tick queue during world generation here, so skip it
                    Set(level, placePos, placeAir ? air : fluid);
                }
            }
        }
        var barrier = config.Barrier.GetState(level, random, origin2);
        if (!barrier.Owner.IsAir)
        {
            for (var xx = 0; xx < 16; xx++)
            {
                for (var zz = 0; zz < 16; zz++)
                {
                    for (var yy = 0; yy < 8; yy++)
                    {
                        if (!IsShell(xx, zz, yy, grid)) continue;
                        if (yy >= 4 && random.NextInt(2) == 0) continue;
                        var offset = origin2.Offset(xx, yy, zz);
                        if (!IsSolid(VegetationSupport.Get(level, offset))) continue;
                        if (!config.CanReplaceWithBarrier.Test(level, offset)) continue;
                        Set(level, offset, barrier);
                    }
                }
            }
        }
        //Vanilla freezes the surface of water lakes; the ice check depends on biome temperature, which the world generation context cannot reach here, so skip that part
        return true;
    }

    //IsShell the cell is outside the ellipsoid but has a neighbor inside it, i.e. a shell, maps to vanilla check
    private static bool IsShell(int xx, int zz, int yy, bool[] grid)
    {
        if (grid[GridIndex(xx, zz, yy)]) return false;
        if (xx < 15 && grid[GridIndex(xx + 1, zz, yy)]) return true;
        if (xx > 0 && grid[GridIndex(xx - 1, zz, yy)]) return true;
        if (zz < 15 && grid[GridIndex(xx, zz + 1, yy)]) return true;
        if (zz > 0 && grid[GridIndex(xx, zz - 1, yy)]) return true;
        if (yy < 7 && grid[GridIndex(xx, zz, yy + 1)]) return true;
        return yy > 0 && grid[GridIndex(xx, zz, yy - 1)];
    }

    //GridIndex flatten the 3D grid to 1D, maps to vanilla ((xx * 16) + zz) * 8 + yy
    private static int GridIndex(int xx, int zz, int yy) => ((xx * 16) + zz) * 8 + yy;

    //IsSolid whether the state counts as solid, maps to vanilla isSolid
    //NetCraft has no material system, so approximate with "has collision, is not air and carries no fluid"
    private static bool IsSolid(BlockState state)
        => !state.Owner.IsAir && state.FluidState.IsEmpty
            && state.Owner is BlockBehaviour behaviour && behaviour.HasCollision;

    private static void Set(WorldGenRegion level, BlockPos pos, BlockState state)
        => level.SetBlockState(pos.X, pos.Y, pos.Z, state);
}
