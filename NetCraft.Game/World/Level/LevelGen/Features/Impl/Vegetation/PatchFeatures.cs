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

//CaveSurfaceCodec 洞穴表面朝向编解码 对应原版 CaveSurface.CODEC 的 floor/ceiling 名
internal sealed class CaveSurfaceCodec : ScalarCodec<CaveSurface>
{
    public static readonly CaveSurfaceCodec Instance = new();

    public override DataResult<CaveSurface> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (!text.Result().IsPresent) return DataResult<CaveSurface>.Error(() => "surface 必须是字符串");
        var surface = CaveSurfaceExtensions.FromSerializedName(text.GetOrThrow());
        return surface is { } value
            ? DataResult<CaveSurface>.Success(value)
            : DataResult<CaveSurface>.Error(() => $"未知的 surface: {text.GetOrThrow()}");
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, CaveSurface value)
        => DataResult<U>.Success(ops.CreateString(value.GetSerializedName()));
}

//VegetationPatchConfiguration 植被斑块配置 对应原版 VegetationPatchConfiguration
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

//VegetationPatchFeature 植被斑块特征 对应原版 VegetationPatchFeature
//先按半径铺一层地表方块 再在每根成功铺到地面的柱子上按概率放植被
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

    //PlaceGroundPatch 逐列定位地表并向下替换成地表方块 返回铺成功的柱顶 对应原版 placeGroundPatch
    //原版用 HashSet 收集 这里改成插入序列表 迭代顺序才能确定
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

    //DistributeVegetation 每根柱顶按概率放植被 对应原版 distributeVegetation
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

    //PlaceVegetation 在柱顶朝外那一格放植被 对应原版 placeVegetation
    protected virtual bool PlaceVegetation(WorldGenRegion level, VegetationPatchConfiguration config,
        ChunkGenerator generator, RandomSource random, BlockPos vegetationPos)
        => PlacedFeatureHelpers.Place(config.VegetationFeature, level, generator, random,
            vegetationPos.Offset(config.Surface.GetDirection().Opposite));

    //PlaceGround 从柱顶向外逐格替换成地表方块 遇见不可替换的方块提前收手 对应原版 placeGround
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

//WaterloggedVegetationPatchFeature 含水植被斑块特征 对应原版 WaterloggedVegetationPatchFeature
//铺完地面后把没有被侧面或下方露出的柱顶灌满水 放植被时再把方块标成含水
public sealed class WaterloggedVegetationPatchFeature : VegetationPatchFeature
{
    private const string FeatureId = "waterlogged_vegetation_patch";

    //名字不能与基类的 Instance 重名 否则派生类会把它遮住
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

    //IsExposed 四水平向或下方有任一面没被整面遮住就算露出 对应原版 isExposed
    private static bool IsExposed(WorldGenRegion level, BlockPos pos)
        => IsExposedDirection(level, pos, Direction.North) || IsExposedDirection(level, pos, Direction.East)
            || IsExposedDirection(level, pos, Direction.South) || IsExposedDirection(level, pos, Direction.West)
            || IsExposedDirection(level, pos, Direction.Down);

    //IsExposedDirection 该面朝向的邻格没有顶住本格就算露出 对应原版 isExposedDirection
    private static bool IsExposedDirection(WorldGenRegion level, BlockPos pos, Direction direction)
    {
        var neighbourPos = pos.Offset(direction);
        return !VegetationSupport.IsFaceSturdy(VegetationSupport.Get(level, neighbourPos), direction.Opposite);
    }

    protected override bool PlaceVegetation(WorldGenRegion level, VegetationPatchConfiguration config,
        ChunkGenerator generator, RandomSource random, BlockPos placementPos)
    {
        //原版拿柱顶下一格去放植被 再回看柱顶自身是否需要标成含水
        if (!base.PlaceVegetation(level, config, generator, random, placementPos.Offset(Direction.Down)))
            return false;
        var placed = VegetationSupport.Get(level, placementPos);
        if (VegetationSupport.HasProperty(placed, "waterlogged") && !placed.GetValue(BlockStateProperties.Waterlogged))
            VegetationSupport.Set(level, placementPos,
                VegetationSupport.WithProperty(placed, "waterlogged", true));
        return true;
    }
}

//MultifaceGrowthConfiguration 多面生长配置 对应原版 MultifaceGrowthConfiguration
//validDirections 按天花板 地面 墙面三个开关的声明序拼出来 洗牌顺序依赖它
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

    //GetShuffledDirections 可放置方向洗牌副本 对应原版 getShuffledDirections
    public List<Direction> GetShuffledDirections(RandomSource random)
        => VegetationSupport.ShuffledCopy(_validDirections, random);

    //GetShuffledDirectionsExcept 排除某方向后洗牌副本 对应原版 getShuffledDirectionsExcept
    public List<Direction> GetShuffledDirectionsExcept(RandomSource random, Direction excludeDirection)
    {
        var filtered = new List<Direction>();
        foreach (var direction in _validDirections)
            if (direction != excludeDirection) filtered.Add(direction);
        return VegetationSupport.ShuffledCopy(filtered, random);
    }
}

//MultifaceGrowthFeature 多面生长特征 对应原版 MultifaceGrowthFeature
//先在原点试放 不成再沿每个可放置方向逐格搜索空气或同类方块
//原版放置后会调用方块自己的扩散器 本作没有该机制 只保留放置与扩散概率的随机消耗
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
                //原版自原点沿搜索方向逐格推进 第 i 轮落在第 i+1 格
                var pos = origin.Relative(searchDirection, i + 1);
                var state = VegetationSupport.Get(level, pos);
                if (!IsAirOrWater(state) && !VegetationSupport.IsState(state, config.PlaceBlock.Id.Path)) continue;
                if (PlaceGrowthIfPossible(level, pos, state, config, random, placementDirections)) return true;
            }
        }
        return false;
    }

    //IsAirOrWater 空气或水 对应原版 isAirOrWater
    private static bool IsAirOrWater(BlockState state)
        => state.Owner.IsAir || VegetationSupport.IsState(state, "water");

    //PlaceGrowthIfPossible 逐方向找一个能依附的邻居并落子 对应原版 placeGrowthIfPossible
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
            //原版命中扩散概率时会驱动方块的扩散器 本作没有该机制 只保留概率本身的随机消耗
            if (random.NextFloat() < config.ChanceOfSpreading) return true;
            return true;
        }
        return false;
    }

    //StateForPlacement 按旧状态算出带朝向面的新状态 对应原版 MultifaceBlock.getStateForPlacement
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

    //IsValidStateForPlacement 该面还没长且邻居顶得住这一面 对应原版 isValidStateForPlacement
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

//RootSystemConfiguration 根系配置 对应原版 RootSystemConfiguration
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

//RootSystemFeature 根系特征 对应原版 RootSystemFeature
//先沿水柱向上找能长树的位置 放好树后把从原点到树位之间换成扎根土 最后在下方播撒垂根
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

    //PlaceDirtAndTree 自原点向上逐格找落树点 找到就放树并铺扎根土 对应原版 placeDirtAndTree
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

    //SpaceForTree 上方足够高且四周同一水平面无高差 对应原版 spaceForTree
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

    //IsAllowedTreeSpace 空气可以 水最多没过指定格数 对应原版 isAllowedTreeSpace
    private static bool IsAllowedTreeSpace(BlockState state, int blocksAboveOrigin, int allowedVerticalWaterHeight)
    {
        if (state.Owner.IsAir) return true;
        var blocksAboveGround = blocksAboveOrigin + 1;
        return blocksAboveGround <= allowedVerticalWaterHeight && VegetationSupport.IsState(state, "water");
    }

    //PlaceDirt 把原点到树位之间的柱子换成扎根土 对应原版 placeDirt
    private static void PlaceDirt(BlockPos origin, int targetHeight, WorldGenRegion level,
        RootSystemConfiguration config, RandomSource random)
    {
        for (var y = origin.Y; y < targetHeight; y++)
            PlaceRootedDirt(level, config, random, origin.X, origin.Z, new BlockPos(origin.X, y, origin.Z));
    }

    //PlaceRootedDirt 在半径内随机找可替换位置换扎根土 对应原版 placeRootedDirt
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

    //PlaceRoots 在原点周围随机播撒垂根 对应原版 placeRoots
    //原版还判了垂根的 canSurvive 本作没有该方块的存活判定 只用上方顶面实心条件
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
