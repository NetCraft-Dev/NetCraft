using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Util.Random;
using PrimDirection = NetCraft.Primitives.Direction;
using RegBlock = NetCraft.Registry.Block;

namespace NetCraft.Game.World.Level.LevelGen.Features.Impl.Trees;

//RootPlacerType 树根放置器类型基类 对应原版 RootPlacerType<P>
public abstract class RootPlacerType : NetCraft.Registry.RootPlacerType<object>
{
    public Identifier Id { get; }

    protected RootPlacerType(Identifier id) => Id = id;

    public abstract DataResult<RootPlacer> Decode<U>(DynamicOps<U> ops, MapLike<U> input);

    public abstract void EncodeFields<U>(DynamicOps<U> ops, RootPlacer value, RecordBuilder<U> builder);
}

//RootPlacerType<P> 具体放置器类型的泛型中间层
public abstract class RootPlacerType<P> : RootPlacerType where P : RootPlacer
{
    private readonly MapCodec<P> _codec;

    protected RootPlacerType(Identifier id, MapCodec<P> codec) : base(id) => _codec = codec;

    public sealed override DataResult<RootPlacer> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => _codec.Decode(ops, input).Map(value => (RootPlacer)value);

    public sealed override void EncodeFields<U>(DynamicOps<U> ops, RootPlacer value, RecordBuilder<U> builder)
    {
        if (value is P typed) _codec.EncodeTo(ops, typed, builder);
    }
}

internal sealed class SimpleRootPlacerType<P> : RootPlacerType<P> where P : RootPlacer
{
    public SimpleRootPlacerType(string id, MapCodec<P> codec)
        : base(Identifier.WithDefaultNamespace(id), codec) { }
}

//RootPlacerTypes 内置树根放置器类型登记 对应原版 RootPlacerType 的静态字段
public static class RootPlacerTypes
{
    public static readonly RootPlacerType<MangroveRootPlacer> Mangrove =
        Register("mangrove_root_placer", MangroveRootPlacer.Codec);

    private static RootPlacerType<T> Register<T>(string path, MapCodec<T> codec) where T : RootPlacer
    {
        var type = new SimpleRootPlacerType<T>(path, codec);
        Registry<NetCraft.Registry.RootPlacerType<object>>.Register(
            BuiltInRegistries.ROOT_PLACER_TYPE, type.Id, type);
        return type;
    }
}

//RootPlacerParts 树根放置器共用字段 对应原版 rootPlacerParts
internal static class RootPlacerParts
{
    public static readonly MapCodec<IntProvider> TrunkOffsetY = IntProviders.Codec.FieldOf("trunk_offset_y");
    public static readonly MapCodec<BlockStateProvider> RootProvider =
        BlockStateProvider.Codec.FieldOf("root_provider");
    //字段名避开类型名 否则同名的静态字段会遮蔽 AboveRootPlacement 类型
    public static readonly MapCodec<Optional<AboveRootPlacement>> AboveRoot =
        AboveRootPlacement.Codec.OptionalFieldOf("above_root_placement");
}

//RootPlacer 树根放置器基类 对应原版 RootPlacer
public abstract class RootPlacer
{
    public static readonly Codec<RootPlacer> Codec = new RootPlacerDispatchCodec();

    protected IntProvider TrunkOffsetY { get; }
    protected BlockStateProvider RootProvider { get; }
    protected Optional<AboveRootPlacement> AboveRootPlacement { get; }

    protected RootPlacer(IntProvider trunkOffsetY, BlockStateProvider rootProvider,
        Optional<AboveRootPlacement> aboveRootPlacement)
    {
        TrunkOffsetY = trunkOffsetY;
        RootProvider = rootProvider;
        AboveRootPlacement = aboveRootPlacement;
    }

    public abstract RootPlacerType Type { get; }

    public abstract bool PlaceRoots(WorldGenRegion level, Action<BlockPos, BlockState> rootSetter,
        RandomSource random, BlockPos origin, BlockPos trunkOrigin, TreeConfiguration config);

    //CanPlaceRoot 该位置可放根 对应原版 canPlaceRoot
    protected virtual bool CanPlaceRoot(WorldGenRegion level, BlockPos pos) => TreeUtil.ValidTreePos(level, pos);

    //PlaceRoot 放一格根并按概率在其上方补一格 对应原版 placeRoot
    protected virtual void PlaceRoot(WorldGenRegion level, Action<BlockPos, BlockState> rootSetter,
        RandomSource random, BlockPos pos, TreeConfiguration config)
    {
        if (!CanPlaceRoot(level, pos)) return;
        rootSetter(pos, GetPotentiallyWaterloggedState(level, pos, RootProvider.GetState(level, random, pos)));
        if (!AboveRootPlacement.IsPresent) return;
        var abovePlacement = AboveRootPlacement.Get();
        var above = pos.Offset(0, 1, 0);
        if (random.NextFloat() < abovePlacement.AboveRootPlacementChance
            && level.GetBlockState(above.X, above.Y, above.Z).Owner.IsAir)
            rootSetter(above, GetPotentiallyWaterloggedState(level, above,
                abovePlacement.AboveRootProvider.GetState(level, random, above)));
    }

    //GetPotentiallyWaterloggedState 有 waterlogged 属性就按位置是否含水写上 对应原版同名方法
    protected static BlockState GetPotentiallyWaterloggedState(WorldGenRegion level, BlockPos pos,
        BlockState state)
    {
        if (!state.HasProperty(TreeUtil.Waterlogged)) return state;
        return state.SetValue(TreeUtil.Waterlogged, TreeUtil.IsWaterAt(level, pos));
    }

    //GetTrunkOrigin 树干原点相对树根原点抬升一段 对应原版 getTrunkOrigin
    public BlockPos GetTrunkOrigin(BlockPos origin, RandomSource random)
        => origin.Offset(0, TrunkOffsetY.Sample(random), 0);
}

//RootPlacerDispatchCodec 按 type 字段查 ROOT_PLACER_TYPE 再委派给该类型
internal sealed class RootPlacerDispatchCodec : ScalarCodec<RootPlacer>
{
    public override DataResult<RootPlacer> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => DecodePlacer(ops, map));

    private static DataResult<RootPlacer> DecodePlacer<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var typeTag = input.Get("type");
        if (!typeTag.IsPresent) return DataResult<RootPlacer>.Error(() => "树根放置器缺 type 字段");
        var typeText = ops.GetStringValue(typeTag.Get());
        if (!typeText.Result().IsPresent)
            return DataResult<RootPlacer>.Error(() => "树根放置器的 type 必须是字符串");
        var typeId = Identifier.TryParse(typeText.GetOrThrow());
        if (typeId is null)
            return DataResult<RootPlacer>.Error(() => $"非法的放置器类型: {typeText.GetOrThrow()}");
        if (BuiltInRegistries.ROOT_PLACER_TYPE.GetValue(typeId.Value) is not RootPlacerType type)
            return DataResult<RootPlacer>.Error(() => $"未知的树根放置器类型: {typeId}");
        return type.Decode(ops, input);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, RootPlacer value)
    {
        var builder = ops.MapBuilder();
        builder.Add("type", ops.CreateString(value.Type.Id.ToString()));
        value.Type.EncodeFields(ops, value, builder);
        return builder.Build(ops.Empty());
    }
}

//AboveRootPlacement 根上方补放方块的设置 对应原版 AboveRootPlacement
public sealed class AboveRootPlacement
{
    public static readonly Codec<AboveRootPlacement> Codec =
        RecordCodecBuilder.Of2<AboveRootPlacement, BlockStateProvider, float>(
            BlockStateProvider.Codec.FieldOf("above_root_provider")
                .ForGetter<AboveRootPlacement, BlockStateProvider>(p => p.AboveRootProvider),
            Codecs.Float.FieldOf("above_root_placement_chance")
                .ForGetter<AboveRootPlacement, float>(p => p.AboveRootPlacementChance),
            (provider, chance) => new AboveRootPlacement(provider, chance));

    public BlockStateProvider AboveRootProvider { get; }
    public float AboveRootPlacementChance { get; }

    public AboveRootPlacement(BlockStateProvider aboveRootProvider, float aboveRootPlacementChance)
    {
        AboveRootProvider = aboveRootProvider;
        AboveRootPlacementChance = aboveRootPlacementChance;
    }
}

//MangroveRootPlacement 红树根参数 对应原版 MangroveRootPlacement
public sealed class MangroveRootPlacement
{
    public static readonly Codec<MangroveRootPlacement> Codec =
        RecordCodecBuilder.Of6<MangroveRootPlacement, HolderSet<RegBlock>, HolderSet<RegBlock>,
            BlockStateProvider, int, int, float>(
            HolderSetCodecs.BlockSet.FieldOf("can_grow_through")
                .ForGetter<MangroveRootPlacement, HolderSet<RegBlock>>(p => p.CanGrowThrough),
            HolderSetCodecs.BlockSet.FieldOf("muddy_roots_in")
                .ForGetter<MangroveRootPlacement, HolderSet<RegBlock>>(p => p.MuddyRootsIn),
            BlockStateProvider.Codec.FieldOf("muddy_roots_provider")
                .ForGetter<MangroveRootPlacement, BlockStateProvider>(p => p.MuddyRootsProvider),
            Codecs.Int.FieldOf("max_root_width").ForGetter<MangroveRootPlacement, int>(p => p.MaxRootWidth),
            Codecs.Int.FieldOf("max_root_length").ForGetter<MangroveRootPlacement, int>(p => p.MaxRootLength),
            Codecs.Float.FieldOf("random_skew_chance")
                .ForGetter<MangroveRootPlacement, float>(p => p.RandomSkewChance),
            (canGrowThrough, muddyRootsIn, muddyRootsProvider, maxRootWidth, maxRootLength, randomSkewChance) =>
                new MangroveRootPlacement(canGrowThrough, muddyRootsIn, muddyRootsProvider, maxRootWidth,
                    maxRootLength, randomSkewChance));

    public HolderSet<RegBlock> CanGrowThrough { get; }
    public HolderSet<RegBlock> MuddyRootsIn { get; }
    public BlockStateProvider MuddyRootsProvider { get; }
    public int MaxRootWidth { get; }
    public int MaxRootLength { get; }
    public float RandomSkewChance { get; }

    public MangroveRootPlacement(HolderSet<RegBlock> canGrowThrough, HolderSet<RegBlock> muddyRootsIn,
        BlockStateProvider muddyRootsProvider, int maxRootWidth, int maxRootLength, float randomSkewChance)
    {
        CanGrowThrough = canGrowThrough;
        MuddyRootsIn = muddyRootsIn;
        MuddyRootsProvider = muddyRootsProvider;
        MaxRootWidth = maxRootWidth;
        MaxRootLength = maxRootLength;
        RandomSkewChance = randomSkewChance;
    }
}

//MangroveRootPlacer 红树根放置器 对应原版 MangroveRootPlacer
public sealed class MangroveRootPlacer : RootPlacer
{
    public static readonly MapCodec<MangroveRootPlacer> Codec =
        RecordCodecBuilder.Of4<MangroveRootPlacer, IntProvider, BlockStateProvider,
            Optional<AboveRootPlacement>, MangroveRootPlacement>(
            RootPlacerParts.TrunkOffsetY.ForGetter<MangroveRootPlacer, IntProvider>(c => c.TrunkOffsetY),
            RootPlacerParts.RootProvider.ForGetter<MangroveRootPlacer, BlockStateProvider>(c => c.RootProvider),
            RootPlacerParts.AboveRoot
                .ForGetter<MangroveRootPlacer, Optional<AboveRootPlacement>>(c => c.AboveRootPlacement),
            MangroveRootPlacement.Codec.FieldOf("mangrove_root_placement")
                .ForGetter<MangroveRootPlacer, MangroveRootPlacement>(c => c.RootPlacement),
            (trunkOffsetY, rootProvider, aboveRootPlacement, mangroveRootPlacement) =>
                new MangroveRootPlacer(trunkOffsetY, rootProvider, aboveRootPlacement, mangroveRootPlacement));

    public MangroveRootPlacement RootPlacement { get; }

    public MangroveRootPlacer(IntProvider trunkOffsetY, BlockStateProvider rootProvider,
        Optional<AboveRootPlacement> aboveRootPlacement, MangroveRootPlacement mangroveRootPlacement)
        : base(trunkOffsetY, rootProvider, aboveRootPlacement)
        => RootPlacement = mangroveRootPlacement;

    public override RootPlacerType Type => RootPlacerTypes.Mangrove;

    public override bool PlaceRoots(WorldGenRegion level, Action<BlockPos, BlockState> rootSetter,
        RandomSource random, BlockPos origin, BlockPos trunkOrigin, TreeConfiguration config)
    {
        var rootPositions = new List<BlockPos>();
        var columnPos = origin;
        while (columnPos.Y < trunkOrigin.Y)
        {
            if (!CanPlaceRoot(level, columnPos)) return false;
            columnPos = columnPos.Offset(0, 1, 0);
        }
        rootPositions.Add(trunkOrigin.Offset(0, -1, 0));
        foreach (var dir in TreeUtil.Horizontal)
        {
            var pos = trunkOrigin.Offset(dir);
            var positionsInDirection = new List<BlockPos>();
            if (!SimulateRoots(level, random, pos, dir, trunkOrigin, positionsInDirection, 0)) return false;
            rootPositions.AddRange(positionsInDirection);
            rootPositions.Add(trunkOrigin.Offset(dir));
        }
        foreach (var rootPos in rootPositions)
            PlaceRoot(level, rootSetter, random, rootPos, config);
        return true;
    }

    //SimulateRoots 递归铺开四个方向上的根 对应原版 simulateRoots
    private bool SimulateRoots(WorldGenRegion level, RandomSource random, BlockPos rootPos,
        PrimDirection dir, BlockPos rootOrigin, List<BlockPos> rootPositions, int layer)
    {
        var maxRootLength = RootPlacement.MaxRootLength;
        if (layer == maxRootLength || rootPositions.Count > maxRootLength) return false;
        var potentialRootPositions = PotentialRootPositions(rootPos, dir, random, rootOrigin);
        foreach (var pos in potentialRootPositions)
        {
            if (!CanPlaceRoot(level, pos)) continue;
            rootPositions.Add(pos);
            if (!SimulateRoots(level, random, pos, dir, rootOrigin, rootPositions, layer + 1)) return false;
        }
        return true;
    }

    //PotentialRootPositions 下一步可能的根位置 对应原版 potentialRootPositions
    protected List<BlockPos> PotentialRootPositions(BlockPos pos, PrimDirection prevDir, RandomSource random,
        BlockPos rootOrigin)
    {
        var below = pos.Offset(0, -1, 0);
        var nextTo = pos.Offset(prevDir);
        var width = TreeUtil.DistManhattan(pos, rootOrigin);
        var maxRootWidth = RootPlacement.MaxRootWidth;
        var randomSkewChance = RootPlacement.RandomSkewChance;
        if (width > maxRootWidth - 3 && width <= maxRootWidth)
            return random.NextFloat() < randomSkewChance
                ? new List<BlockPos> { below, nextTo.Offset(0, -1, 0) }
                : new List<BlockPos> { below };
        if (width > maxRootWidth) return new List<BlockPos> { below };
        if (random.NextFloat() < randomSkewChance) return new List<BlockPos> { below };
        return random.NextBoolean()
            ? new List<BlockPos> { nextTo }
            : new List<BlockPos> { below };
    }

    protected override bool CanPlaceRoot(WorldGenRegion level, BlockPos pos)
        => base.CanPlaceRoot(level, pos)
           || RootPlacement.CanGrowThrough.Contains(
               BuiltInRegistries.BLOCK.WrapAsHolder(level.GetBlockState(pos.X, pos.Y, pos.Z).Owner));

    protected override void PlaceRoot(WorldGenRegion level, Action<BlockPos, BlockState> rootSetter,
        RandomSource random, BlockPos pos, TreeConfiguration config)
    {
        if (RootPlacement.MuddyRootsIn.Contains(
                BuiltInRegistries.BLOCK.WrapAsHolder(level.GetBlockState(pos.X, pos.Y, pos.Z).Owner)))
        {
            var muddyRoots = RootPlacement.MuddyRootsProvider.GetState(level, random, pos);
            rootSetter(pos, GetPotentiallyWaterloggedState(level, pos, muddyRoots));
        }
        else
        {
            base.PlaceRoot(level, rootSetter, random, pos, config);
        }
    }
}
