using NetCraft.Codec;
using NetCraft.Game.World.Level.Block;
using NetCraft.Game.World.Level.LevelGen.BlockPredicates;
using NetCraft.Game.World.Level.LevelGen.Features.Impl;
using NetCraft.Game.World.Level.LevelGen.Placement;
using NetCraft.Game.World.Level.LevelGen.Structure;
using NetCraft.Game.World.Phys.Collision;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Util;
using NetCraft.Util.Random;
using RegBlock = NetCraft.Registry.Block;
using RegistryConfiguredFeature = NetCraft.Registry.ConfiguredFeature;
using RegistryPlacedFeature = NetCraft.Registry.PlacedFeature;

namespace NetCraft.Game.World.Level.LevelGen.Features.Impl.Vegetation;

//CaveSurfaceCodec cave surface orientation codec, maps to the floor/ceiling names of vanilla CaveSurface.CODEC
internal sealed class CaveSurfaceCodec : ScalarCodec<CaveSurface>
{
    public static readonly CaveSurfaceCodec Instance = new();

    public override DataResult<CaveSurface> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (!text.Result().IsPresent) return DataResult<CaveSurface>.Error(() => "surface must be a string");
        var surface = CaveSurfaceExtensions.FromSerializedName(text.GetOrThrow());
        return surface is { } value
            ? DataResult<CaveSurface>.Success(value)
            : DataResult<CaveSurface>.Error(() => $"unknown surface: {text.GetOrThrow()}");
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, CaveSurface value)
        => DataResult<U>.Success(ops.CreateString(value.GetSerializedName()));
}

//VegetationPatchConfiguration vegetation patch configuration, maps to vanilla VegetationPatchConfiguration
public sealed class VegetationPatchConfiguration : FeatureConfiguration
{
    public static readonly Codec<VegetationPatchConfiguration> Codec =
        RecordCodecBuilder.Of10<VegetationPatchConfiguration, HolderSet<RegBlock>, BlockStateProvider,
            Holder<RegistryPlacedFeature>, CaveSurface, IntProvider, float, int, float, IntProvider, float>(
            HolderSetCodecs.BlockSet.FieldOf("replaceable")
                .ForGetter<VegetationPatchConfiguration, HolderSet<RegBlock>>(c => c.Replaceable),
            BlockStateProvider.Codec.FieldOf("ground_state")
                .ForGetter<VegetationPatchConfiguration, BlockStateProvider>(c => c.GroundState),
            PlacedFeatureInlineRefCodec.Instance.FieldOf("vegetation_feature")
                .ForGetter<VegetationPatchConfiguration, Holder<RegistryPlacedFeature>>(c => c.VegetationFeature),
            CaveSurfaceCodec.Instance.FieldOf("surface")
                .ForGetter<VegetationPatchConfiguration, CaveSurface>(c => c.Surface),
            IntProviders.Codec.FieldOf("depth")
                .ForGetter<VegetationPatchConfiguration, IntProvider>(c => c.Depth),
            Codecs.Float.FieldOf("extra_bottom_block_chance")
                .ForGetter<VegetationPatchConfiguration, float>(c => c.ExtraBottomBlockChance),
            Codecs.Int.FieldOf("vertical_range")
                .ForGetter<VegetationPatchConfiguration, int>(c => c.VerticalRange),
            Codecs.Float.FieldOf("vegetation_chance")
                .ForGetter<VegetationPatchConfiguration, float>(c => c.VegetationChance),
            IntProviders.Codec.FieldOf("xz_radius")
                .ForGetter<VegetationPatchConfiguration, IntProvider>(c => c.XzRadius),
            Codecs.Float.FieldOf("extra_edge_column_chance")
                .ForGetter<VegetationPatchConfiguration, float>(c => c.ExtraEdgeColumnChance),
            (replaceable, groundState, vegetationFeature, surface, depth, extraBottomBlockChance, verticalRange,
                vegetationChance, xzRadius, extraEdgeColumnChance) => new VegetationPatchConfiguration(replaceable,
                groundState, vegetationFeature, surface, depth, extraBottomBlockChance, verticalRange,
                vegetationChance, xzRadius, extraEdgeColumnChance));

    public HolderSet<RegBlock> Replaceable { get; }
    public BlockStateProvider GroundState { get; }
    public Holder<RegistryPlacedFeature> VegetationFeature { get; }
    public CaveSurface Surface { get; }
    public IntProvider Depth { get; }
    public float ExtraBottomBlockChance { get; }
    public int VerticalRange { get; }
    public float VegetationChance { get; }
    public IntProvider XzRadius { get; }
    public float ExtraEdgeColumnChance { get; }

    public VegetationPatchConfiguration(HolderSet<RegBlock> replaceable, BlockStateProvider groundState,
        Holder<RegistryPlacedFeature> vegetationFeature, CaveSurface surface, IntProvider depth,
        float extraBottomBlockChance, int verticalRange, float vegetationChance, IntProvider xzRadius,
        float extraEdgeColumnChance)
    {
        Replaceable = replaceable;
        GroundState = groundState;
        VegetationFeature = vegetationFeature;
        Surface = surface;
        Depth = depth;
        ExtraBottomBlockChance = extraBottomBlockChance;
        VerticalRange = verticalRange;
        VegetationChance = vegetationChance;
        XzRadius = xzRadius;
        ExtraEdgeColumnChance = extraEdgeColumnChance;
    }

    public override IEnumerable<Holder<RegistryConfiguredFeature>> SubFeatures
        => PlacedFeatureHelpers.SubFeatures(VegetationFeature);
}

//VegetationPatchFeature vegetation patch feature, maps to vanilla VegetationPatchFeature
//First lays a layer of ground blocks within the radius, then places vegetation by chance on each column that reached the ground
public class VegetationPatchFeature : Feature<VegetationPatchConfiguration>
{
    private const string FeatureId = "vegetation_patch";

    public static readonly VegetationPatchFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId),
        new VegetationPatchFeature(Identifier.WithDefaultNamespace(FeatureId)));

    protected VegetationPatchFeature(Identifier id)
        : base(id, VegetationPatchConfiguration.Codec) { }

    protected override bool Place(VegetationPatchConfiguration config, FeaturePlaceContext context)
    {
        var level = context.Level;
        var random = context.Random;
        var origin = context.Origin;
        var xRadius = config.XzRadius.Sample(random) + 1;
        var zRadius = config.XzRadius.Sample(random) + 1;
        var surface = PlaceGroundPatch(level, config, random, origin, xRadius, zRadius);
        DistributeVegetation(context, level, config, random, surface);
        return surface.Count > 0;
    }

    //PlaceGroundPatch locate the ground per column and replace downward with ground blocks, returning the successful column tops, maps to vanilla placeGroundPatch
    //Vanilla collects into a HashSet; an insertion-ordered list is used here so the iteration order is deterministic
    protected virtual List<BlockPos> PlaceGroundPatch(WorldGenRegion level, VegetationPatchConfiguration config,
        RandomSource random, BlockPos origin, int xRadius, int zRadius)
    {
        var inwards = config.Surface.GetDirection();
        var outwards = inwards.Opposite;
        var surface = new List<BlockPos>();
        for (var dx = -xRadius; dx <= xRadius; dx++)
        {
            var isXEdge = dx == -xRadius || dx == xRadius;
            for (var dz = -zRadius; dz <= zRadius; dz++)
            {
                var isZEdge = dz == -zRadius || dz == zRadius;
                var isCorner = isXEdge && isZEdge;
                var isEdgeButNotCorner = (isXEdge || isZEdge) && !isCorner;
                if (isCorner) continue;
                if (isEdgeButNotCorner)
                {
                    if (config.ExtraEdgeColumnChance == 0.0f) continue;
                    if (random.NextFloat() > config.ExtraEdgeColumnChance) continue;
                }
                var pos = origin.Offset(dx, 0, dz);
                for (var offset = 0;
                     VegetationSupport.IsAir(level, pos) && offset < config.VerticalRange;
                     offset++) pos = pos.Offset(inwards);
                for (var offset = 0;
                     !VegetationSupport.IsAir(level, pos) && offset < config.VerticalRange;
                     offset++) pos = pos.Offset(outwards);
                var belowPos = pos.Offset(config.Surface.GetDirection());
                var belowState = VegetationSupport.Get(level, belowPos);
                if (!VegetationSupport.IsAir(level, pos)
                    || !VegetationSupport.IsFaceSturdy(belowState, config.Surface.GetDirection().Opposite)) continue;
                var depth = config.Depth.Sample(random);
                if (config.ExtraBottomBlockChance > 0.0f && random.NextFloat() < config.ExtraBottomBlockChance)
                    depth += 1;
                var groundPos = belowPos;
                if (PlaceGround(level, config, random, ref belowPos, depth)) surface.Add(groundPos);
            }
        }
        return surface;
    }

    //DistributeVegetation place vegetation by chance on each column top, maps to vanilla distributeVegetation
    protected virtual void DistributeVegetation(FeaturePlaceContext context, WorldGenRegion level,
        VegetationPatchConfiguration config, RandomSource random, List<BlockPos> surface)
    {
        foreach (var surfacePos in surface)
        {
            if (config.VegetationChance <= 0.0f) continue;
            if (random.NextFloat() >= config.VegetationChance) continue;
            PlaceVegetation(level, config, context.ChunkGenerator, random, surfacePos);
        }
    }

    //PlaceVegetation place vegetation one cell out from the column top, maps to vanilla placeVegetation
    protected virtual bool PlaceVegetation(WorldGenRegion level, VegetationPatchConfiguration config,
        ChunkGenerator generator, RandomSource random, BlockPos vegetationPos)
        => PlacedFeatureHelpers.Place(config.VegetationFeature, level, generator, random,
            vegetationPos.Offset(config.Surface.GetDirection().Opposite));

    //PlaceGround replace outward from the column top with ground blocks, stopping early at a non-replaceable block, maps to vanilla placeGround
    protected static bool PlaceGround(WorldGenRegion level, VegetationPatchConfiguration config,
        RandomSource random, ref BlockPos belowPos, int depth)
    {
        for (var i = 0; i < depth; i++)
        {
            var stateToPlace = config.GroundState.GetState(level, random, belowPos);
            var belowState = VegetationSupport.Get(level, belowPos);
            if (stateToPlace.Owner != belowState.Owner)
            {
                if (!VegetationSupport.IsInSet(belowState, config.Replaceable)) return i != 0;
                VegetationSupport.Set(level, belowPos, stateToPlace);
                belowPos = belowPos.Offset(config.Surface.GetDirection());
            }
        }
        return true;
    }
}

//WaterloggedVegetationPatchFeature waterlogged vegetation patch feature, maps to vanilla WaterloggedVegetationPatchFeature
//After laying the ground, fills column tops not exposed from the sides or below with water, and marks blocks waterlogged when placing vegetation
public sealed class WaterloggedVegetationPatchFeature : VegetationPatchFeature
{
    private const string FeatureId = "waterlogged_vegetation_patch";

    //The name must not collide with the base class Instance, otherwise the derived class would shadow it
    public static readonly WaterloggedVegetationPatchFeature WaterloggedInstance = Register(
        Identifier.WithDefaultNamespace(FeatureId),
        new WaterloggedVegetationPatchFeature(Identifier.WithDefaultNamespace(FeatureId)));

    private WaterloggedVegetationPatchFeature(Identifier id)
        : base(id) { }

    protected override List<BlockPos> PlaceGroundPatch(WorldGenRegion level, VegetationPatchConfiguration config,
        RandomSource random, BlockPos origin, int xRadius, int zRadius)
    {
        var surface = base.PlaceGroundPatch(level, config, random, origin, xRadius, zRadius);
        var waterSurface = new List<BlockPos>();
        foreach (var surfacePos in surface)
        {
            if (IsExposed(level, surfacePos)) continue;
            waterSurface.Add(surfacePos);
        }
        foreach (var waterPos in waterSurface)
            VegetationSupport.Set(level, waterPos, VegetationSupport.StateOf("water"));
        return waterSurface;
    }

    //IsExposed exposed if any of the four horizontal sides or the bottom is not fully covered, maps to vanilla isExposed
    private static bool IsExposed(WorldGenRegion level, BlockPos pos)
        => IsExposedDirection(level, pos, Direction.North) || IsExposedDirection(level, pos, Direction.East)
            || IsExposedDirection(level, pos, Direction.South) || IsExposedDirection(level, pos, Direction.West)
            || IsExposedDirection(level, pos, Direction.Down);

    //IsExposedDirection exposed when the neighbor in that direction does not support this cell, maps to vanilla isExposedDirection
    private static bool IsExposedDirection(WorldGenRegion level, BlockPos pos, Direction direction)
    {
        var neighbourPos = pos.Offset(direction);
        return !VegetationSupport.IsFaceSturdy(VegetationSupport.Get(level, neighbourPos), direction.Opposite);
    }

    protected override bool PlaceVegetation(WorldGenRegion level, VegetationPatchConfiguration config,
        ChunkGenerator generator, RandomSource random, BlockPos placementPos)
    {
        //Vanilla places vegetation at the cell below the column top, then revisits the top itself to mark it waterlogged
        if (!base.PlaceVegetation(level, config, generator, random, placementPos.Offset(Direction.Down)))
            return false;
        var placed = VegetationSupport.Get(level, placementPos);
        if (VegetationSupport.HasProperty(placed, "waterlogged") && !placed.GetValue(BlockStateProperties.Waterlogged))
            VegetationSupport.Set(level, placementPos,
                VegetationSupport.WithProperty(placed, "waterlogged", true));
        return true;
    }
}

//MultifaceGrowthConfiguration multiface growth configuration, maps to vanilla MultifaceGrowthConfiguration
//validDirections is assembled in the declaration order of the ceiling, floor and wall flags; the shuffle order depends on it
public sealed class MultifaceGrowthConfiguration : FeatureConfiguration
{
    public static readonly Codec<MultifaceGrowthConfiguration> Codec =
        RecordCodecBuilder.Of7<MultifaceGrowthConfiguration, RegBlock, int, bool, bool, bool, float,
            HolderSet<RegBlock>>(
            StructureBlockCodec.Instance.FieldOf("block")
                .ForGetter<MultifaceGrowthConfiguration, RegBlock>(c => c.PlaceBlock),
            Codecs.Int.OptionalFieldOf("search_range", 10)
                .ForGetter<MultifaceGrowthConfiguration, int>(c => c.SearchRange),
            Codecs.Bool.OptionalFieldOf("can_place_on_floor", false)
                .ForGetter<MultifaceGrowthConfiguration, bool>(c => c.CanPlaceOnFloor),
            Codecs.Bool.OptionalFieldOf("can_place_on_ceiling", false)
                .ForGetter<MultifaceGrowthConfiguration, bool>(c => c.CanPlaceOnCeiling),
            Codecs.Bool.OptionalFieldOf("can_place_on_wall", false)
                .ForGetter<MultifaceGrowthConfiguration, bool>(c => c.CanPlaceOnWall),
            Codecs.Float.OptionalFieldOf("chance_of_spreading", 0.5f)
                .ForGetter<MultifaceGrowthConfiguration, float>(c => c.ChanceOfSpreading),
            HolderSetCodecs.BlockSet.FieldOf("can_be_placed_on")
                .ForGetter<MultifaceGrowthConfiguration, HolderSet<RegBlock>>(c => c.CanBePlacedOn),
            (placeBlock, searchRange, canPlaceOnFloor, canPlaceOnCeiling, canPlaceOnWall, chanceOfSpreading,
                canBePlacedOn) => new MultifaceGrowthConfiguration(placeBlock, searchRange, canPlaceOnFloor,
                canPlaceOnCeiling, canPlaceOnWall, chanceOfSpreading, canBePlacedOn));

    public RegBlock PlaceBlock { get; }
    public int SearchRange { get; }
    public bool CanPlaceOnFloor { get; }
    public bool CanPlaceOnCeiling { get; }
    public bool CanPlaceOnWall { get; }
    public float ChanceOfSpreading { get; }
    public HolderSet<RegBlock> CanBePlacedOn { get; }

    private readonly List<Direction> _validDirections = new(6);

    public MultifaceGrowthConfiguration(RegBlock placeBlock, int searchRange, bool canPlaceOnFloor,
        bool canPlaceOnCeiling, bool canPlaceOnWall, float chanceOfSpreading, HolderSet<RegBlock> canBePlacedOn)
    {
        PlaceBlock = placeBlock;
        SearchRange = searchRange;
        CanPlaceOnFloor = canPlaceOnFloor;
        CanPlaceOnCeiling = canPlaceOnCeiling;
        CanPlaceOnWall = canPlaceOnWall;
        ChanceOfSpreading = chanceOfSpreading;
        CanBePlacedOn = canBePlacedOn;
        if (canPlaceOnCeiling) _validDirections.Add(Direction.Up);
        if (canPlaceOnFloor) _validDirections.Add(Direction.Down);
        if (canPlaceOnWall) _validDirections.AddRange(VegetationSupport.HorizontalPlane);
    }

    //GetShuffledDirections shuffled copy of the placeable directions, maps to vanilla getShuffledDirections
    public List<Direction> GetShuffledDirections(RandomSource random)
        => VegetationSupport.ShuffledCopy(_validDirections, random);

    //GetShuffledDirectionsExcept shuffled copy excluding one direction, maps to vanilla getShuffledDirectionsExcept
    public List<Direction> GetShuffledDirectionsExcept(RandomSource random, Direction excludeDirection)
    {
        var filtered = new List<Direction>();
        foreach (var direction in _validDirections)
            if (direction != excludeDirection) filtered.Add(direction);
        return VegetationSupport.ShuffledCopy(filtered, random);
    }
}

//MultifaceGrowthFeature multiface growth feature, maps to vanilla MultifaceGrowthFeature
//Tries the origin first, then searches cell by cell along each placeable direction for air or the same block type
//Vanilla invokes the block's own spreader after placing; there is no such mechanism here, so only the placement and the spread-probability random consumption are kept
public sealed class MultifaceGrowthFeature : Feature<MultifaceGrowthConfiguration>
{
    private const string FeatureId = "multiface_growth";

    public static readonly MultifaceGrowthFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new MultifaceGrowthFeature());

    private MultifaceGrowthFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), MultifaceGrowthConfiguration.Codec) { }

    protected override bool Place(MultifaceGrowthConfiguration config, FeaturePlaceContext context)
    {
        var level = context.Level;
        var origin = context.Origin;
        var random = context.Random;
        var originState = VegetationSupport.Get(level, origin);
        if (!IsAirOrWater(originState)) return false;
        var searchDirections = config.GetShuffledDirections(random);
        if (PlaceGrowthIfPossible(level, origin, originState, config, random, searchDirections)) return true;
        foreach (var searchDirection in searchDirections)
        {
            var placementDirections = config.GetShuffledDirectionsExcept(random, searchDirection.Opposite);
            for (var i = 0; i < config.SearchRange; i++)
            {
                //Vanilla advances cell by cell from the origin along the search direction, landing on cell i+1 on round i
                var pos = origin.Relative(searchDirection, i + 1);
                var state = VegetationSupport.Get(level, pos);
                if (!IsAirOrWater(state) && !VegetationSupport.IsState(state, config.PlaceBlock.Id.Path)) continue;
                if (PlaceGrowthIfPossible(level, pos, state, config, random, placementDirections)) return true;
            }
        }
        return false;
    }

    //IsAirOrWater air or water, maps to vanilla isAirOrWater
    private static bool IsAirOrWater(BlockState state)
        => state.Owner.IsAir || VegetationSupport.IsState(state, "water");

    //PlaceGrowthIfPossible find one attachable neighbor per direction and place there, maps to vanilla placeGrowthIfPossible
    public static bool PlaceGrowthIfPossible(WorldGenRegion level, BlockPos pos, BlockState oldState,
        MultifaceGrowthConfiguration config, RandomSource random, List<Direction> placementDirections)
    {
        foreach (var placementDirection in placementDirections)
        {
            var neighbourState = VegetationSupport.Get(level, pos.Offset(placementDirection));
            if (!VegetationSupport.IsInSet(neighbourState, config.CanBePlacedOn)) continue;
            var newState = StateForPlacement(oldState, level, pos, placementDirection, config.PlaceBlock);
            if (newState is not { } toPlace) return false;
            VegetationSupport.Set(level, pos, toPlace);
            //Vanilla drives the block spreader when the spread roll hits; there is no such mechanism here, so only the random consumption of the roll is kept
            if (random.NextFloat() < config.ChanceOfSpreading) return true;
            return true;
        }
        return false;
    }

    //StateForPlacement derive the new state with the facing face from the old state, maps to vanilla MultifaceBlock.getStateForPlacement
    private static BlockState? StateForPlacement(BlockState oldState, WorldGenRegion level, BlockPos placePos,
        Direction placementDirection, RegBlock placeBlock)
    {
        if (!IsValidStateForPlacement(level, oldState, placePos, placementDirection, placeBlock)) return null;
        var newState = VegetationSupport.IsState(oldState, placeBlock.Id.Path)
            ? oldState
            : VegetationSupport.IsState(oldState, "water")
                ? VegetationSupport.WithProperty(placeBlock.DefaultBlockState, "waterlogged", true)
                : placeBlock.DefaultBlockState;
        return VegetationSupport.WithProperty(newState, VegetationSupport.FaceName(placementDirection), true);
    }

    //IsValidStateForPlacement the face has not grown yet and the neighbor supports it, maps to vanilla isValidStateForPlacement
    private static bool IsValidStateForPlacement(WorldGenRegion level, BlockState oldState, BlockPos placementPos,
        Direction placementDirection, RegBlock placeBlock)
    {
        var faceName = VegetationSupport.FaceName(placementDirection);
        if (VegetationSupport.IsState(oldState, placeBlock.Id.Path)
            && VegetationSupport.WithProperty(oldState, faceName, true) == oldState) return false;
        var neighbourPos = placementPos.Offset(placementDirection);
        var neighbourState = VegetationSupport.Get(level, neighbourPos);
        if (neighbourState.Owner is not BlockBehaviour behaviour) return false;
        var support = behaviour.GetBlockSupportShape(neighbourState, EmptyBlockGetter.Instance, neighbourPos);
        if (RegBlock.IsFaceFull(support, placementDirection.Opposite)) return true;
        var collision = behaviour.GetCollisionShape(neighbourState, EmptyBlockGetter.Instance, neighbourPos,
            CollisionContext.Empty);
        return RegBlock.IsFaceFull(collision, placementDirection.Opposite);
    }
}

//RootSystemConfiguration root system configuration, maps to vanilla RootSystemConfiguration
public sealed class RootSystemConfiguration : FeatureConfiguration
{
    public static readonly Codec<RootSystemConfiguration> Codec =
        RecordCodecBuilder.Of15<RootSystemConfiguration, Holder<RegistryPlacedFeature>, int, int, int, int,
            HolderSet<RegBlock>, BlockStateProvider, int, int, int, int, BlockStateProvider, int, int,
            BlockPredicate>(
            PlacedFeatureInlineRefCodec.Instance.FieldOf("feature")
                .ForGetter<RootSystemConfiguration, Holder<RegistryPlacedFeature>>(c => c.TreeFeature),
            Codecs.Int.FieldOf("required_vertical_space_for_tree")
                .ForGetter<RootSystemConfiguration, int>(c => c.RequiredVerticalSpaceForTree),
            Codecs.Int.FieldOf("level_test_distance")
                .ForGetter<RootSystemConfiguration, int>(c => c.LevelTestDistance),
            Codecs.Int.FieldOf("max_level_deviation")
                .ForGetter<RootSystemConfiguration, int>(c => c.MaxLevelDeviation),
            Codecs.Int.FieldOf("root_radius").ForGetter<RootSystemConfiguration, int>(c => c.RootRadius),
            HolderSetCodecs.BlockSet.FieldOf("root_replaceable")
                .ForGetter<RootSystemConfiguration, HolderSet<RegBlock>>(c => c.RootReplaceable),
            BlockStateProvider.Codec.FieldOf("root_state_provider")
                .ForGetter<RootSystemConfiguration, BlockStateProvider>(c => c.RootStateProvider),
            Codecs.Int.FieldOf("root_placement_attempts")
                .ForGetter<RootSystemConfiguration, int>(c => c.RootPlacementAttempts),
            Codecs.Int.FieldOf("root_column_max_height")
                .ForGetter<RootSystemConfiguration, int>(c => c.RootColumnMaxHeight),
            Codecs.Int.FieldOf("hanging_root_radius")
                .ForGetter<RootSystemConfiguration, int>(c => c.HangingRootRadius),
            Codecs.Int.FieldOf("hanging_roots_vertical_span")
                .ForGetter<RootSystemConfiguration, int>(c => c.HangingRootsVerticalSpan),
            BlockStateProvider.Codec.FieldOf("hanging_root_state_provider")
                .ForGetter<RootSystemConfiguration, BlockStateProvider>(c => c.HangingRootStateProvider),
            Codecs.Int.FieldOf("hanging_root_placement_attempts")
                .ForGetter<RootSystemConfiguration, int>(c => c.HangingRootPlacementAttempts),
            Codecs.Int.FieldOf("allowed_vertical_water_for_tree")
                .ForGetter<RootSystemConfiguration, int>(c => c.AllowedVerticalWaterForTree),
            BlockPredicate.Codec.FieldOf("allowed_tree_position")
                .ForGetter<RootSystemConfiguration, BlockPredicate>(c => c.AllowedTreePosition),
            (treeFeature, requiredVerticalSpaceForTree, levelTestDistance, maxLevelDeviation, rootRadius,
                rootReplaceable, rootStateProvider, rootPlacementAttempts, rootColumnMaxHeight, hangingRootRadius,
                hangingRootsVerticalSpan, hangingRootStateProvider, hangingRootPlacementAttempts,
                allowedVerticalWaterForTree, allowedTreePosition) => new RootSystemConfiguration(treeFeature,
                requiredVerticalSpaceForTree, levelTestDistance, maxLevelDeviation, rootRadius, rootReplaceable,
                rootStateProvider, rootPlacementAttempts, rootColumnMaxHeight, hangingRootRadius,
                hangingRootsVerticalSpan, hangingRootStateProvider, hangingRootPlacementAttempts,
                allowedVerticalWaterForTree, allowedTreePosition));

    public Holder<RegistryPlacedFeature> TreeFeature { get; }
    public int RequiredVerticalSpaceForTree { get; }
    public int LevelTestDistance { get; }
    public int MaxLevelDeviation { get; }
    public int RootRadius { get; }
    public HolderSet<RegBlock> RootReplaceable { get; }
    public BlockStateProvider RootStateProvider { get; }
    public int RootPlacementAttempts { get; }
    public int RootColumnMaxHeight { get; }
    public int HangingRootRadius { get; }
    public int HangingRootsVerticalSpan { get; }
    public BlockStateProvider HangingRootStateProvider { get; }
    public int HangingRootPlacementAttempts { get; }
    public int AllowedVerticalWaterForTree { get; }
    public BlockPredicate AllowedTreePosition { get; }

    public RootSystemConfiguration(Holder<RegistryPlacedFeature> treeFeature, int requiredVerticalSpaceForTree,
        int levelTestDistance, int maxLevelDeviation, int rootRadius, HolderSet<RegBlock> rootReplaceable,
        BlockStateProvider rootStateProvider, int rootPlacementAttempts, int rootColumnMaxHeight,
        int hangingRootRadius, int hangingRootsVerticalSpan, BlockStateProvider hangingRootStateProvider,
        int hangingRootPlacementAttempts, int allowedVerticalWaterForTree, BlockPredicate allowedTreePosition)
    {
        TreeFeature = treeFeature;
        RequiredVerticalSpaceForTree = requiredVerticalSpaceForTree;
        LevelTestDistance = levelTestDistance;
        MaxLevelDeviation = maxLevelDeviation;
        RootRadius = rootRadius;
        RootReplaceable = rootReplaceable;
        RootStateProvider = rootStateProvider;
        RootPlacementAttempts = rootPlacementAttempts;
        RootColumnMaxHeight = rootColumnMaxHeight;
        HangingRootRadius = hangingRootRadius;
        HangingRootsVerticalSpan = hangingRootsVerticalSpan;
        HangingRootStateProvider = hangingRootStateProvider;
        HangingRootPlacementAttempts = hangingRootPlacementAttempts;
        AllowedVerticalWaterForTree = allowedVerticalWaterForTree;
        AllowedTreePosition = allowedTreePosition;
    }

    public override IEnumerable<Holder<RegistryConfiguredFeature>> SubFeatures
        => PlacedFeatureHelpers.SubFeatures(TreeFeature);
}

//RootSystemFeature root system feature, maps to vanilla RootSystemFeature
//Searches up the water column for a tree spot, places the tree, replaces the column from origin to tree with rooted dirt, then scatters hanging roots below
public sealed class RootSystemFeature : Feature<RootSystemConfiguration>
{
    private const string FeatureId = "root_system";

    public static readonly RootSystemFeature Instance = Register(
        Identifier.WithDefaultNamespace(FeatureId), new RootSystemFeature());

    private RootSystemFeature()
        : base(Identifier.WithDefaultNamespace(FeatureId), RootSystemConfiguration.Codec) { }

    protected override bool Place(RootSystemConfiguration config, FeaturePlaceContext context)
    {
        var level = context.Level;
        var origin = context.Origin;
        if (!VegetationSupport.Get(level, origin).Owner.IsAir) return false;
        var random = context.Random;
        var workingPos = origin;
        if (PlaceDirtAndTree(level, context.ChunkGenerator, config, random, ref workingPos, origin))
            PlaceRoots(level, config, random, origin);
        return true;
    }

    //PlaceDirtAndTree search upward from the origin for a tree spot; on success place the tree and lay rooted dirt, maps to vanilla placeDirtAndTree
    private static bool PlaceDirtAndTree(WorldGenRegion level, ChunkGenerator generator,
        RootSystemConfiguration config, RandomSource random, ref BlockPos workingPos, BlockPos origin)
    {
        for (var y = 0; y < config.RootColumnMaxHeight; y++)
        {
            workingPos = workingPos.Offset(Direction.Up);
            if (level.GetHeight(Heightmap.Types.WorldSurface, workingPos.X, workingPos.Z) < workingPos.Y) return false;
            if (!config.AllowedTreePosition.Test(level, workingPos) || !SpaceForTree(level, config, workingPos))
                continue;
            var belowPos = workingPos.Offset(Direction.Down);
            if (VegetationSupport.IsState(VegetationSupport.Get(level, belowPos), "lava")
                || !VegetationSupport.IsFaceSturdy(VegetationSupport.Get(level, belowPos), Direction.Up)) return false;
            if (!PlacedFeatureHelpers.Place(config.TreeFeature, level, generator, random, workingPos)) continue;
            PlaceDirt(origin, origin.Y + y, level, config, random);
            return true;
        }
        return false;
    }

    //SpaceForTree enough height above and no height difference around the same level, maps to vanilla spaceForTree
    private static bool SpaceForTree(WorldGenRegion level, RootSystemConfiguration config, BlockPos pos)
    {
        var columnUpPos = pos;
        for (var i = 1; i <= config.RequiredVerticalSpaceForTree; i++)
        {
            columnUpPos = columnUpPos.Offset(Direction.Up);
            if (!IsAllowedTreeSpace(VegetationSupport.Get(level, columnUpPos), i,
                    config.AllowedVerticalWaterForTree)) return false;
        }
        if (config.LevelTestDistance <= 0) return true;
        var cornerPos = pos;
        for (var i = 0; i < 4; i++)
        {
            cornerPos = cornerPos.Relative(VegetationSupport.From2DDataValue(i), config.LevelTestDistance);
            var below = VegetationSupport.Get(level, cornerPos.Offset(0, -config.MaxLevelDeviation, 0));
            var above = VegetationSupport.Get(level, cornerPos.Offset(0, config.MaxLevelDeviation, 0));
            if (below.Owner.IsAir || !above.Owner.IsAir) return false;
            cornerPos = pos;
        }
        return true;
    }

    //IsAllowedTreeSpace air is fine; water may cover at most the given number of cells, maps to vanilla isAllowedTreeSpace
    private static bool IsAllowedTreeSpace(BlockState state, int blocksAboveOrigin, int allowedVerticalWaterHeight)
    {
        if (state.Owner.IsAir) return true;
        var blocksAboveGround = blocksAboveOrigin + 1;
        return blocksAboveGround <= allowedVerticalWaterHeight && VegetationSupport.IsState(state, "water");
    }

    //PlaceDirt replace the column between origin and tree spot with rooted dirt, maps to vanilla placeDirt
    private static void PlaceDirt(BlockPos origin, int targetHeight, WorldGenRegion level,
        RootSystemConfiguration config, RandomSource random)
    {
        for (var y = origin.Y; y < targetHeight; y++)
            PlaceRootedDirt(level, config, random, origin.X, origin.Z, new BlockPos(origin.X, y, origin.Z));
    }

    //PlaceRootedDirt randomly find replaceable cells within the radius and turn them into rooted dirt, maps to vanilla placeRootedDirt
    private static void PlaceRootedDirt(WorldGenRegion level, RootSystemConfiguration config, RandomSource random,
        int originX, int originZ, BlockPos columnPos)
    {
        var rootRadius = config.RootRadius;
        var workingPos = columnPos;
        for (var i = 0; i < config.RootPlacementAttempts; i++)
        {
            workingPos = workingPos.Offset(random.NextInt(rootRadius) - random.NextInt(rootRadius), 0,
                random.NextInt(rootRadius) - random.NextInt(rootRadius));
            if (VegetationSupport.IsInSet(VegetationSupport.Get(level, workingPos), config.RootReplaceable))
                VegetationSupport.Set(level, workingPos, config.RootStateProvider.GetState(level, random, workingPos));
            workingPos = new BlockPos(originX, workingPos.Y, originZ);
        }
    }

    //PlaceRoots randomly scatter hanging roots around the origin, maps to vanilla placeRoots
    //Vanilla also checks the hanging root's canSurvive; there is no survival check for that block, so only the solid top face above is required
    private static void PlaceRoots(WorldGenRegion level, RootSystemConfiguration config, RandomSource random,
        BlockPos pos)
    {
        var rootRadius = config.HangingRootRadius;
        var verticalSpan = config.HangingRootsVerticalSpan;
        for (var i = 0; i < config.HangingRootPlacementAttempts; i++)
        {
            var target = pos.Offset(random.NextInt(rootRadius) - random.NextInt(rootRadius),
                random.NextInt(verticalSpan) - random.NextInt(verticalSpan),
                random.NextInt(rootRadius) - random.NextInt(rootRadius));
            if (!VegetationSupport.IsAir(level, target)) continue;
            var targetState = config.HangingRootStateProvider.GetState(level, random, target);
            if (!VegetationSupport.IsFaceSturdy(VegetationSupport.Get(level, target.Offset(Direction.Up)),
                    Direction.Down)) continue;
            VegetationSupport.Set(level, target, targetState);
        }
    }
}
