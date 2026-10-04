using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Util;
using NetCraft.Util.Random;
using PrimDirection = NetCraft.Primitives.Direction;

namespace NetCraft.Game.World.Level.LevelGen.Features.Impl.Trees;

//FoliagePlacerType 树叶放置器类型基类 对应原版 FoliagePlacerType<P>
public abstract class FoliagePlacerType : NetCraft.Registry.FoliagePlacerType<object>
{
    public Identifier Id { get; }

    protected FoliagePlacerType(Identifier id) => Id = id;

    public abstract DataResult<FoliagePlacer> Decode<U>(DynamicOps<U> ops, MapLike<U> input);

    public abstract void EncodeFields<U>(DynamicOps<U> ops, FoliagePlacer value, RecordBuilder<U> builder);
}

//FoliagePlacerType<P> 具体放置器类型的泛型中间层
public abstract class FoliagePlacerType<P> : FoliagePlacerType where P : FoliagePlacer
{
    private readonly MapCodec<P> _codec;

    protected FoliagePlacerType(Identifier id, MapCodec<P> codec) : base(id) => _codec = codec;

    public sealed override DataResult<FoliagePlacer> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => _codec.Decode(ops, input).Map(value => (FoliagePlacer)value);

    public sealed override void EncodeFields<U>(DynamicOps<U> ops, FoliagePlacer value, RecordBuilder<U> builder)
    {
        if (value is P typed) _codec.EncodeTo(ops, typed, builder);
    }
}

internal sealed class SimpleFoliagePlacerType<P> : FoliagePlacerType<P> where P : FoliagePlacer
{
    public SimpleFoliagePlacerType(string id, MapCodec<P> codec)
        : base(Identifier.WithDefaultNamespace(id), codec) { }
}

//FoliagePlacerTypes 内置树叶放置器类型登记 对应原版 FoliagePlacerType 的静态字段
public static class FoliagePlacerTypes
{
    public static readonly FoliagePlacerType<BlobFoliagePlacer> Blob =
        Register("blob_foliage_placer", BlobFoliagePlacer.Codec);

    public static readonly FoliagePlacerType<SpruceFoliagePlacer> Spruce =
        Register("spruce_foliage_placer", SpruceFoliagePlacer.Codec);

    public static readonly FoliagePlacerType<PineFoliagePlacer> Pine =
        Register("pine_foliage_placer", PineFoliagePlacer.Codec);

    public static readonly FoliagePlacerType<AcaciaFoliagePlacer> Acacia =
        Register("acacia_foliage_placer", AcaciaFoliagePlacer.Codec);

    public static readonly FoliagePlacerType<DarkOakFoliagePlacer> DarkOak =
        Register("dark_oak_foliage_placer", DarkOakFoliagePlacer.Codec);

    public static readonly FoliagePlacerType<MegaPineFoliagePlacer> MegaPine =
        Register("mega_pine_foliage_placer", MegaPineFoliagePlacer.Codec);

    public static readonly FoliagePlacerType<MegaJungleFoliagePlacer> Jungle =
        Register("jungle_foliage_placer", MegaJungleFoliagePlacer.Codec);

    public static readonly FoliagePlacerType<FancyFoliagePlacer> Fancy =
        Register("fancy_foliage_placer", FancyFoliagePlacer.Codec);

    public static readonly FoliagePlacerType<BushFoliagePlacer> Bush =
        Register("bush_foliage_placer", BushFoliagePlacer.Codec);

    public static readonly FoliagePlacerType<RandomSpreadFoliagePlacer> RandomSpread =
        Register("random_spread_foliage_placer", RandomSpreadFoliagePlacer.Codec);

    public static readonly FoliagePlacerType<CherryFoliagePlacer> Cherry =
        Register("cherry_foliage_placer", CherryFoliagePlacer.Codec);

    private static FoliagePlacerType<T> Register<T>(string path, MapCodec<T> codec) where T : FoliagePlacer
    {
        var type = new SimpleFoliagePlacerType<T>(path, codec);
        Registry<NetCraft.Registry.FoliagePlacerType<object>>.Register(
            BuiltInRegistries.FOLIAGE_PLACER_TYPE, type.Id, type);
        return type;
    }
}

//FoliagePlacerParts 树叶放置器共用的半径与偏移字段 对应原版 foliagePlacerParts
internal static class FoliagePlacerParts
{
    public static readonly MapCodec<IntProvider> Radius = IntProviders.Codec.FieldOf("radius");
    public static readonly MapCodec<IntProvider> Offset = IntProviders.Codec.FieldOf("offset");
}

//FoliagePlacer 树叶放置器基类 对应原版 FoliagePlacer
public abstract class FoliagePlacer
{
    public static readonly Codec<FoliagePlacer> Codec = new FoliagePlacerDispatchCodec();

    protected IntProvider Radius { get; }
    protected IntProvider OffsetProvider { get; }

    protected FoliagePlacer(IntProvider radius, IntProvider offset)
    {
        Radius = radius;
        OffsetProvider = offset;
    }

    //FoliageSetter 树叶落位回调 对应原版 FoliageSetter
    //IsSet 供垂枝判断上方是否已有树叶
    public sealed class FoliageSetter
    {
        private readonly Action<BlockPos, BlockState> _set;
        private readonly Func<BlockPos, bool> _isSet;

        public FoliageSetter(Action<BlockPos, BlockState> set, Func<BlockPos, bool> isSet)
        {
            _set = set;
            _isSet = isSet;
        }

        public void Set(BlockPos pos, BlockState state) => _set(pos, state);

        public bool IsSet(BlockPos pos) => _isSet(pos);
    }

    //FoliageAttachment 树叶挂点 对应原版 FoliageAttachment
    public sealed class FoliageAttachment
    {
        public BlockPos Pos { get; }
        public int RadiusOffset { get; }
        public bool DoubleTrunk { get; }

        public FoliageAttachment(BlockPos pos, int radiusOffset, bool doubleTrunk)
        {
            Pos = pos;
            RadiusOffset = radiusOffset;
            DoubleTrunk = doubleTrunk;
        }
    }

    public abstract FoliagePlacerType Type { get; }

    protected abstract void CreateFoliage(WorldGenRegion level, FoliageSetter foliageSetter, RandomSource random,
        TreeConfiguration config, int treeHeight, FoliageAttachment foliageAttachment, int foliageHeight,
        int leafRadius, int offset);

    public abstract int FoliageHeight(RandomSource random, int treeHeight, TreeConfiguration config);

    protected abstract bool ShouldSkipLocation(RandomSource random, int dx, int y, int dz, int currentRadius,
        bool doubleTrunk);

    //CreateFoliage 取一次随机偏移后落到具体实现 对应原版 createFoliage 八参重载
    public void CreateFoliage(WorldGenRegion level, FoliageSetter foliageSetter, RandomSource random,
        TreeConfiguration config, int treeHeight, FoliageAttachment foliageAttachment, int foliageHeight,
        int leafRadius)
        => CreateFoliage(level, foliageSetter, random, config, treeHeight, foliageAttachment, foliageHeight,
            leafRadius, OffsetProvider.Sample(random));

    //FoliageRadius 树冠半径 对应原版 foliageRadius
    public virtual int FoliageRadius(RandomSource random, int trunkHeight) => Radius.Sample(random);

    //ShouldSkipLocationSigned 先按双干把镜像距离折半再判定 对应原版 shouldSkipLocationSigned
    protected virtual bool ShouldSkipLocationSigned(RandomSource random, int dx, int y, int dz, int currentRadius,
        bool doubleTrunk)
    {
        int minDx, minDz;
        if (doubleTrunk)
        {
            minDx = Math.Min(Math.Abs(dx), Math.Abs(dx - 1));
            minDz = Math.Min(Math.Abs(dz), Math.Abs(dz - 1));
        }
        else
        {
            minDx = Math.Abs(dx);
            minDz = Math.Abs(dz);
        }
        return ShouldSkipLocation(random, minDx, y, minDz, currentRadius, doubleTrunk);
    }

    //PlaceLeavesRow 铺一层树叶 对应原版 placeLeavesRow
    protected void PlaceLeavesRow(WorldGenRegion level, FoliageSetter foliageSetter, RandomSource random,
        TreeConfiguration config, BlockPos origin, int currentRadius, int y, bool doubleTrunk)
    {
        var offset = doubleTrunk ? 1 : 0;
        for (var dx = -currentRadius; dx <= currentRadius + offset; dx++)
        {
            for (var dz = -currentRadius; dz <= currentRadius + offset; dz++)
            {
                if (ShouldSkipLocationSigned(random, dx, y, dz, currentRadius, doubleTrunk)) continue;
                TryPlaceLeaf(level, foliageSetter, random, config, origin.Offset(dx, y, dz));
            }
        }
    }

    //PlaceLeavesRowWithHangingLeavesBelow 铺一层树叶并沿四周往下挂叶 对应原版同名方法
    protected void PlaceLeavesRowWithHangingLeavesBelow(WorldGenRegion level, FoliageSetter foliageSetter,
        RandomSource random, TreeConfiguration config, BlockPos origin, int currentRadius, int y, bool doubleTrunk,
        float hangingLeavesChance, float hangingLeavesExtensionChance)
    {
        PlaceLeavesRow(level, foliageSetter, random, config, origin, currentRadius, y, doubleTrunk);
        var offset = doubleTrunk ? 1 : 0;
        var logPos = origin.Offset(0, -1, 0);
        foreach (var alongEdge in TreeUtil.Horizontal)
        {
            var toEdge = alongEdge.ClockWise;
            var offsetToEdge = toEdge.AxisDir == PrimDirection.AxisDirection.Positive
                ? currentRadius + offset
                : currentRadius;
            var pos = origin.Offset(0, y - 1, 0)
                .Offset(toEdge.StepX * offsetToEdge, 0, toEdge.StepZ * offsetToEdge)
                .Offset(alongEdge.StepX * -currentRadius, 0, alongEdge.StepZ * -currentRadius);
            var offsetAlongEdge = -currentRadius;
            while (offsetAlongEdge < currentRadius + offset)
            {
                pos = pos.Offset(0, 1, 0);
                var leavesAbove = foliageSetter.IsSet(pos);
                pos = pos.Offset(0, -1, 0);
                if (leavesAbove
                    && TryPlaceExtension(level, foliageSetter, random, config, hangingLeavesChance, logPos, pos))
                {
                    pos = pos.Offset(0, -1, 0);
                    TryPlaceExtension(level, foliageSetter, random, config, hangingLeavesExtensionChance, logPos,
                        pos);
                    pos = pos.Offset(0, 1, 0);
                }
                offsetAlongEdge++;
                pos = pos.Offset(alongEdge);
            }
        }
    }

    //TryPlaceExtension 挂叶候选 离树干太近或掷骰失败就不放 对应原版 tryPlaceExtension
    private static bool TryPlaceExtension(WorldGenRegion level, FoliageSetter foliageSetter, RandomSource random,
        TreeConfiguration config, float chance, BlockPos logPos, BlockPos pos)
    {
        if (TreeUtil.DistManhattan(pos, logPos) >= 7 || random.NextFloat() > chance) return false;
        return TryPlaceLeaf(level, foliageSetter, random, config, pos);
    }

    //TryPlaceLeaf 尝试放一格树叶 已是持久叶或位置不可占用则跳过 对应原版 tryPlaceLeaf
    protected static bool TryPlaceLeaf(WorldGenRegion level, FoliageSetter foliageSetter, RandomSource random,
        TreeConfiguration config, BlockPos pos)
    {
        var isPersistent = level.GetBlockState(pos.X, pos.Y, pos.Z).GetValueOrElse(TreeUtil.Persistent, false);
        if (isPersistent || !TreeUtil.ValidTreePos(level, pos)) return false;
        var foliageState = config.FoliageProvider.GetState(level, random, pos);
        if (foliageState.HasProperty(TreeUtil.Waterlogged))
            foliageState = foliageState.SetValue(TreeUtil.Waterlogged, TreeUtil.IsWaterAt(level, pos));
        foliageSetter.Set(pos, foliageState);
        return true;
    }
}

//FoliagePlacerDispatchCodec 按 type 字段查 FOLIAGE_PLACER_TYPE 再委派给该类型
internal sealed class FoliagePlacerDispatchCodec : ScalarCodec<FoliagePlacer>
{
    public override DataResult<FoliagePlacer> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => DecodePlacer(ops, map));

    private static DataResult<FoliagePlacer> DecodePlacer<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var typeTag = input.Get("type");
        if (!typeTag.IsPresent) return DataResult<FoliagePlacer>.Error(() => "树叶放置器缺 type 字段");
        var typeText = ops.GetStringValue(typeTag.Get());
        if (!typeText.Result().IsPresent)
            return DataResult<FoliagePlacer>.Error(() => "树叶放置器的 type 必须是字符串");
        var typeId = Identifier.TryParse(typeText.GetOrThrow());
        if (typeId is null)
            return DataResult<FoliagePlacer>.Error(() => $"非法的放置器类型: {typeText.GetOrThrow()}");
        if (BuiltInRegistries.FOLIAGE_PLACER_TYPE.GetValue(typeId.Value) is not FoliagePlacerType type)
            return DataResult<FoliagePlacer>.Error(() => $"未知的树叶放置器类型: {typeId}");
        return type.Decode(ops, input);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, FoliagePlacer value)
    {
        var builder = ops.MapBuilder();
        builder.Add("type", ops.CreateString(value.Type.Id.ToString()));
        value.Type.EncodeFields(ops, value, builder);
        return builder.Build(ops.Empty());
    }
}

//BlobFoliagePlacer 团状树冠 对应原版 BlobFoliagePlacer
public class BlobFoliagePlacer : FoliagePlacer
{
    public static readonly MapCodec<BlobFoliagePlacer> Codec =
        RecordCodecBuilder.Of3<BlobFoliagePlacer, IntProvider, IntProvider, int>(
            FoliagePlacerParts.Radius.ForGetter<BlobFoliagePlacer, IntProvider>(p => p.Radius),
            FoliagePlacerParts.Offset.ForGetter<BlobFoliagePlacer, IntProvider>(p => p.OffsetProvider),
            Codecs.Int.FieldOf("height").ForGetter<BlobFoliagePlacer, int>(p => p.Height),
            (radius, offset, height) => new BlobFoliagePlacer(radius, offset, height));

    protected int Height { get; }

    public BlobFoliagePlacer(IntProvider radius, IntProvider offset, int height)
        : base(radius, offset) => Height = height;

    public override FoliagePlacerType Type => FoliagePlacerTypes.Blob;

    protected override void CreateFoliage(WorldGenRegion level, FoliageSetter foliageSetter, RandomSource random,
        TreeConfiguration config, int treeHeight, FoliageAttachment foliageAttachment, int foliageHeight,
        int leafRadius, int offset)
    {
        for (var yo = offset; yo >= offset - foliageHeight; yo--)
        {
            var currentRadius = Math.Max(foliageAttachment.RadiusOffset + leafRadius - 1 - (yo / 2), 0);
            PlaceLeavesRow(level, foliageSetter, random, config, foliageAttachment.Pos, currentRadius, yo,
                foliageAttachment.DoubleTrunk);
        }
    }

    public override int FoliageHeight(RandomSource random, int treeHeight, TreeConfiguration config) => Height;

    protected override bool ShouldSkipLocation(RandomSource random, int dx, int y, int dz, int currentRadius,
        bool doubleTrunk)
        => dx == currentRadius && dz == currentRadius && (random.NextInt(2) == 0 || y == 0);
}

//SpruceFoliagePlacer 云杉圆锥树冠 对应原版 SpruceFoliagePlacer
public sealed class SpruceFoliagePlacer : FoliagePlacer
{
    public static readonly MapCodec<SpruceFoliagePlacer> Codec =
        RecordCodecBuilder.Of3<SpruceFoliagePlacer, IntProvider, IntProvider, IntProvider>(
            FoliagePlacerParts.Radius.ForGetter<SpruceFoliagePlacer, IntProvider>(p => p.Radius),
            FoliagePlacerParts.Offset.ForGetter<SpruceFoliagePlacer, IntProvider>(p => p.OffsetProvider),
            IntProviders.Codec.FieldOf("trunk_height")
                .ForGetter<SpruceFoliagePlacer, IntProvider>(p => p.TrunkHeight),
            (radius, offset, trunkHeight) => new SpruceFoliagePlacer(radius, offset, trunkHeight));

    public IntProvider TrunkHeight { get; }

    public SpruceFoliagePlacer(IntProvider radius, IntProvider offset, IntProvider trunkHeight)
        : base(radius, offset) => TrunkHeight = trunkHeight;

    public override FoliagePlacerType Type => FoliagePlacerTypes.Spruce;

    protected override void CreateFoliage(WorldGenRegion level, FoliageSetter foliageSetter, RandomSource random,
        TreeConfiguration config, int treeHeight, FoliageAttachment foliageAttachment, int foliageHeight,
        int leafRadius, int offset)
    {
        var foliagePos = foliageAttachment.Pos;
        var currentRadius = random.NextInt(2);
        var maxRadius = 1;
        var minRadius = 0;
        for (var yo = offset; yo >= -foliageHeight; yo--)
        {
            PlaceLeavesRow(level, foliageSetter, random, config, foliagePos, currentRadius, yo,
                foliageAttachment.DoubleTrunk);
            if (currentRadius >= maxRadius)
            {
                currentRadius = minRadius;
                minRadius = 1;
                maxRadius = Math.Min(maxRadius + 1, leafRadius + foliageAttachment.RadiusOffset);
            }
            else
            {
                currentRadius++;
            }
        }
    }

    public override int FoliageHeight(RandomSource random, int treeHeight, TreeConfiguration config)
        => Math.Max(4, treeHeight - TrunkHeight.Sample(random));

    protected override bool ShouldSkipLocation(RandomSource random, int dx, int y, int dz, int currentRadius,
        bool doubleTrunk)
        => dx == currentRadius && dz == currentRadius && currentRadius > 0;
}

//PineFoliagePlacer 松树树冠 对应原版 PineFoliagePlacer
public sealed class PineFoliagePlacer : FoliagePlacer
{
    public static readonly MapCodec<PineFoliagePlacer> Codec =
        RecordCodecBuilder.Of3<PineFoliagePlacer, IntProvider, IntProvider, IntProvider>(
            FoliagePlacerParts.Radius.ForGetter<PineFoliagePlacer, IntProvider>(p => p.Radius),
            FoliagePlacerParts.Offset.ForGetter<PineFoliagePlacer, IntProvider>(p => p.OffsetProvider),
            IntProviders.Codec.FieldOf("height").ForGetter<PineFoliagePlacer, IntProvider>(p => p.Height),
            (radius, offset, height) => new PineFoliagePlacer(radius, offset, height));

    public IntProvider Height { get; }

    public PineFoliagePlacer(IntProvider radius, IntProvider offset, IntProvider height)
        : base(radius, offset) => Height = height;

    public override FoliagePlacerType Type => FoliagePlacerTypes.Pine;

    protected override void CreateFoliage(WorldGenRegion level, FoliageSetter foliageSetter, RandomSource random,
        TreeConfiguration config, int treeHeight, FoliageAttachment foliageAttachment, int foliageHeight,
        int leafRadius, int offset)
    {
        var currentRadius = 0;
        for (var yo = offset; yo >= offset - foliageHeight; yo--)
        {
            PlaceLeavesRow(level, foliageSetter, random, config, foliageAttachment.Pos, currentRadius, yo,
                foliageAttachment.DoubleTrunk);
            if (currentRadius >= 1 && yo == offset - foliageHeight + 1) currentRadius--;
            else if (currentRadius < leafRadius + foliageAttachment.RadiusOffset) currentRadius++;
        }
    }

    public override int FoliageRadius(RandomSource random, int trunkHeight)
        => base.FoliageRadius(random, trunkHeight) + random.NextInt(Math.Max(trunkHeight + 1, 1));

    public override int FoliageHeight(RandomSource random, int treeHeight, TreeConfiguration config)
        => Height.Sample(random);

    protected override bool ShouldSkipLocation(RandomSource random, int dx, int y, int dz, int currentRadius,
        bool doubleTrunk)
        => dx == currentRadius && dz == currentRadius && currentRadius > 0;
}

//AcaciaFoliagePlacer 金合欢扁树冠 对应原版 AcaciaFoliagePlacer
public sealed class AcaciaFoliagePlacer : FoliagePlacer
{
    public static readonly MapCodec<AcaciaFoliagePlacer> Codec =
        RecordCodecBuilder.Of2<AcaciaFoliagePlacer, IntProvider, IntProvider>(
            FoliagePlacerParts.Radius.ForGetter<AcaciaFoliagePlacer, IntProvider>(p => p.Radius),
            FoliagePlacerParts.Offset.ForGetter<AcaciaFoliagePlacer, IntProvider>(p => p.OffsetProvider),
            (radius, offset) => new AcaciaFoliagePlacer(radius, offset));

    public AcaciaFoliagePlacer(IntProvider radius, IntProvider offset) : base(radius, offset) { }

    public override FoliagePlacerType Type => FoliagePlacerTypes.Acacia;

    protected override void CreateFoliage(WorldGenRegion level, FoliageSetter foliageSetter, RandomSource random,
        TreeConfiguration config, int treeHeight, FoliageAttachment foliageAttachment, int foliageHeight,
        int leafRadius, int offset)
    {
        var doubleTrunk = foliageAttachment.DoubleTrunk;
        var foliagePos = foliageAttachment.Pos.Offset(0, offset, 0);
        PlaceLeavesRow(level, foliageSetter, random, config, foliagePos,
            leafRadius + foliageAttachment.RadiusOffset, -1 - foliageHeight, doubleTrunk);
        PlaceLeavesRow(level, foliageSetter, random, config, foliagePos, leafRadius - 1, -foliageHeight,
            doubleTrunk);
        PlaceLeavesRow(level, foliageSetter, random, config, foliagePos,
            leafRadius + foliageAttachment.RadiusOffset - 1, 0, doubleTrunk);
    }

    public override int FoliageHeight(RandomSource random, int treeHeight, TreeConfiguration config) => 0;

    protected override bool ShouldSkipLocation(RandomSource random, int dx, int y, int dz, int currentRadius,
        bool doubleTrunk)
        => y == 0
            ? !((dx <= 1 && dz <= 1) || dx == 0 || dz == 0)
            : dx == currentRadius && dz == currentRadius && currentRadius > 0;
}

//DarkOakFoliagePlacer 深色橡木树冠 对应原版 DarkOakFoliagePlacer
public sealed class DarkOakFoliagePlacer : FoliagePlacer
{
    public static readonly MapCodec<DarkOakFoliagePlacer> Codec =
        RecordCodecBuilder.Of2<DarkOakFoliagePlacer, IntProvider, IntProvider>(
            FoliagePlacerParts.Radius.ForGetter<DarkOakFoliagePlacer, IntProvider>(p => p.Radius),
            FoliagePlacerParts.Offset.ForGetter<DarkOakFoliagePlacer, IntProvider>(p => p.OffsetProvider),
            (radius, offset) => new DarkOakFoliagePlacer(radius, offset));

    public DarkOakFoliagePlacer(IntProvider radius, IntProvider offset) : base(radius, offset) { }

    public override FoliagePlacerType Type => FoliagePlacerTypes.DarkOak;

    protected override void CreateFoliage(WorldGenRegion level, FoliageSetter foliageSetter, RandomSource random,
        TreeConfiguration config, int treeHeight, FoliageAttachment foliageAttachment, int foliageHeight,
        int leafRadius, int offset)
    {
        var pos = foliageAttachment.Pos.Offset(0, offset, 0);
        var doubleTrunk = foliageAttachment.DoubleTrunk;
        if (doubleTrunk)
        {
            PlaceLeavesRow(level, foliageSetter, random, config, pos, leafRadius + 2, -1, doubleTrunk);
            PlaceLeavesRow(level, foliageSetter, random, config, pos, leafRadius + 3, 0, doubleTrunk);
            PlaceLeavesRow(level, foliageSetter, random, config, pos, leafRadius + 2, 1, doubleTrunk);
            if (random.NextBoolean())
                PlaceLeavesRow(level, foliageSetter, random, config, pos, leafRadius, 2, doubleTrunk);
            return;
        }
        PlaceLeavesRow(level, foliageSetter, random, config, pos, leafRadius + 2, -1, doubleTrunk);
        PlaceLeavesRow(level, foliageSetter, random, config, pos, leafRadius + 1, 0, doubleTrunk);
    }

    public override int FoliageHeight(RandomSource random, int treeHeight, TreeConfiguration config) => 4;

    protected override bool ShouldSkipLocationSigned(RandomSource random, int dx, int y, int dz, int currentRadius,
        bool doubleTrunk)
    {
        if (y == 0 && doubleTrunk
            && (dx == -currentRadius || dx >= currentRadius)
            && (dz == -currentRadius || dz >= currentRadius))
            return true;
        return base.ShouldSkipLocationSigned(random, dx, y, dz, currentRadius, doubleTrunk);
    }

    protected override bool ShouldSkipLocation(RandomSource random, int dx, int y, int dz, int currentRadius,
        bool doubleTrunk)
        => y != -1 || doubleTrunk
            ? y == 1 && dx + dz > (currentRadius * 2) - 2
            : dx == currentRadius && dz == currentRadius;
}

//MegaJungleFoliagePlacer 巨丛林树冠 注册名 jungle_foliage_placer 对应原版 MegaJungleFoliagePlacer
public sealed class MegaJungleFoliagePlacer : FoliagePlacer
{
    public static readonly MapCodec<MegaJungleFoliagePlacer> Codec =
        RecordCodecBuilder.Of3<MegaJungleFoliagePlacer, IntProvider, IntProvider, int>(
            FoliagePlacerParts.Radius.ForGetter<MegaJungleFoliagePlacer, IntProvider>(p => p.Radius),
            FoliagePlacerParts.Offset.ForGetter<MegaJungleFoliagePlacer, IntProvider>(p => p.OffsetProvider),
            Codecs.Int.FieldOf("height").ForGetter<MegaJungleFoliagePlacer, int>(p => p.Height),
            (radius, offset, height) => new MegaJungleFoliagePlacer(radius, offset, height));

    protected int Height { get; }

    public MegaJungleFoliagePlacer(IntProvider radius, IntProvider offset, int height)
        : base(radius, offset) => Height = height;

    public override FoliagePlacerType Type => FoliagePlacerTypes.Jungle;

    protected override void CreateFoliage(WorldGenRegion level, FoliageSetter foliageSetter, RandomSource random,
        TreeConfiguration config, int treeHeight, FoliageAttachment foliageAttachment, int foliageHeight,
        int leafRadius, int offset)
    {
        var leafHeight = foliageAttachment.DoubleTrunk ? foliageHeight : 1 + random.NextInt(2);
        for (var yo = offset; yo >= offset - leafHeight; yo--)
        {
            var currentRadius = leafRadius + foliageAttachment.RadiusOffset + 1 - yo;
            PlaceLeavesRow(level, foliageSetter, random, config, foliageAttachment.Pos, currentRadius, yo,
                foliageAttachment.DoubleTrunk);
        }
    }

    public override int FoliageHeight(RandomSource random, int treeHeight, TreeConfiguration config) => Height;

    protected override bool ShouldSkipLocation(RandomSource random, int dx, int y, int dz, int currentRadius,
        bool doubleTrunk)
        => dx + dz >= 7 || (dx * dx) + (dz * dz) > currentRadius * currentRadius;
}

//MegaPineFoliagePlacer 巨型松树锯齿树冠 对应原版 MegaPineFoliagePlacer
public sealed class MegaPineFoliagePlacer : FoliagePlacer
{
    public static readonly MapCodec<MegaPineFoliagePlacer> Codec =
        RecordCodecBuilder.Of3<MegaPineFoliagePlacer, IntProvider, IntProvider, IntProvider>(
            FoliagePlacerParts.Radius.ForGetter<MegaPineFoliagePlacer, IntProvider>(p => p.Radius),
            FoliagePlacerParts.Offset.ForGetter<MegaPineFoliagePlacer, IntProvider>(p => p.OffsetProvider),
            IntProviders.Codec.FieldOf("crown_height")
                .ForGetter<MegaPineFoliagePlacer, IntProvider>(p => p.CrownHeight),
            (radius, offset, crownHeight) => new MegaPineFoliagePlacer(radius, offset, crownHeight));

    public IntProvider CrownHeight { get; }

    public MegaPineFoliagePlacer(IntProvider radius, IntProvider offset, IntProvider crownHeight)
        : base(radius, offset) => CrownHeight = crownHeight;

    public override FoliagePlacerType Type => FoliagePlacerTypes.MegaPine;

    protected override void CreateFoliage(WorldGenRegion level, FoliageSetter foliageSetter, RandomSource random,
        TreeConfiguration config, int treeHeight, FoliageAttachment foliageAttachment, int foliageHeight,
        int leafRadius, int offset)
    {
        var foliagePos = foliageAttachment.Pos;
        var prevRadius = 0;
        for (var yy = foliagePos.Y - foliageHeight + offset; yy <= foliagePos.Y + offset; yy++)
        {
            var yo = foliagePos.Y - yy;
            var smoothRadius = leafRadius + foliageAttachment.RadiusOffset
                               + Mth.Floor(yo / foliageHeight * 3.5f);
            var jaggedRadius = yo > 0 && smoothRadius == prevRadius && (yy & 1) == 0
                ? smoothRadius + 1
                : smoothRadius;
            PlaceLeavesRow(level, foliageSetter, random, config, new BlockPos(foliagePos.X, yy, foliagePos.Z),
                jaggedRadius, 0, foliageAttachment.DoubleTrunk);
            prevRadius = smoothRadius;
        }
    }

    public override int FoliageHeight(RandomSource random, int treeHeight, TreeConfiguration config)
        => CrownHeight.Sample(random);

    protected override bool ShouldSkipLocation(RandomSource random, int dx, int y, int dz, int currentRadius,
        bool doubleTrunk)
        => dx + dz >= 7 || (dx * dx) + (dz * dz) > currentRadius * currentRadius;
}

//FancyFoliagePlacer 大橡木球状树冠 对应原版 FancyFoliagePlacer
public sealed class FancyFoliagePlacer : BlobFoliagePlacer
{
    public static readonly MapCodec<FancyFoliagePlacer> Codec =
        RecordCodecBuilder.Of3<FancyFoliagePlacer, IntProvider, IntProvider, int>(
            FoliagePlacerParts.Radius.ForGetter<FancyFoliagePlacer, IntProvider>(p => p.Radius),
            FoliagePlacerParts.Offset.ForGetter<FancyFoliagePlacer, IntProvider>(p => p.OffsetProvider),
            Codecs.Int.FieldOf("height").ForGetter<FancyFoliagePlacer, int>(p => p.Height),
            (radius, offset, height) => new FancyFoliagePlacer(radius, offset, height));

    public FancyFoliagePlacer(IntProvider radius, IntProvider offset, int height)
        : base(radius, offset, height) { }

    public override FoliagePlacerType Type => FoliagePlacerTypes.Fancy;

    protected override void CreateFoliage(WorldGenRegion level, FoliageSetter foliageSetter, RandomSource random,
        TreeConfiguration config, int treeHeight, FoliageAttachment foliageAttachment, int foliageHeight,
        int leafRadius, int offset)
    {
        var yo = offset;
        while (yo >= offset - foliageHeight)
        {
            var currentRadius = leafRadius + (yo == offset || yo == offset - foliageHeight ? 0 : 1);
            PlaceLeavesRow(level, foliageSetter, random, config, foliageAttachment.Pos, currentRadius, yo,
                foliageAttachment.DoubleTrunk);
            yo--;
        }
    }

    public override int FoliageHeight(RandomSource random, int treeHeight, TreeConfiguration config) => Height;

    protected override bool ShouldSkipLocation(RandomSource random, int dx, int y, int dz, int currentRadius,
        bool doubleTrunk)
        => Mth.Square(dx + 0.5f) + Mth.Square(dz + 0.5f) > currentRadius * currentRadius;
}

//BushFoliagePlacer 灌木树冠 对应原版 BushFoliagePlacer
public sealed class BushFoliagePlacer : BlobFoliagePlacer
{
    public static readonly MapCodec<BushFoliagePlacer> Codec =
        RecordCodecBuilder.Of3<BushFoliagePlacer, IntProvider, IntProvider, int>(
            FoliagePlacerParts.Radius.ForGetter<BushFoliagePlacer, IntProvider>(p => p.Radius),
            FoliagePlacerParts.Offset.ForGetter<BushFoliagePlacer, IntProvider>(p => p.OffsetProvider),
            Codecs.Int.FieldOf("height").ForGetter<BushFoliagePlacer, int>(p => p.Height),
            (radius, offset, height) => new BushFoliagePlacer(radius, offset, height));

    public BushFoliagePlacer(IntProvider radius, IntProvider offset, int height)
        : base(radius, offset, height) { }

    public override FoliagePlacerType Type => FoliagePlacerTypes.Bush;

    protected override void CreateFoliage(WorldGenRegion level, FoliageSetter foliageSetter, RandomSource random,
        TreeConfiguration config, int treeHeight, FoliageAttachment foliageAttachment, int foliageHeight,
        int leafRadius, int offset)
    {
        for (var yo = offset; yo >= offset - foliageHeight; yo--)
        {
            var currentRadius = leafRadius + foliageAttachment.RadiusOffset - 1 - yo;
            PlaceLeavesRow(level, foliageSetter, random, config, foliageAttachment.Pos, currentRadius, yo,
                foliageAttachment.DoubleTrunk);
        }
    }

    public override int FoliageHeight(RandomSource random, int treeHeight, TreeConfiguration config) => Height;

    protected override bool ShouldSkipLocation(RandomSource random, int dx, int y, int dz, int currentRadius,
        bool doubleTrunk)
        => dx == currentRadius && dz == currentRadius && random.NextInt(2) == 0;
}

//RandomSpreadFoliagePlacer 撒点式树冠 红树与杜鹃靠它 对应原版 RandomSpreadFoliagePlacer
public sealed class RandomSpreadFoliagePlacer : FoliagePlacer
{
    public static readonly MapCodec<RandomSpreadFoliagePlacer> Codec =
        RecordCodecBuilder.Of4<RandomSpreadFoliagePlacer, IntProvider, IntProvider, IntProvider, int>(
            FoliagePlacerParts.Radius.ForGetter<RandomSpreadFoliagePlacer, IntProvider>(p => p.Radius),
            FoliagePlacerParts.Offset.ForGetter<RandomSpreadFoliagePlacer, IntProvider>(p => p.OffsetProvider),
            IntProviders.Codec.FieldOf("foliage_height")
                .ForGetter<RandomSpreadFoliagePlacer, IntProvider>(p => p.FoliageHeightProvider),
            Codecs.Int.FieldOf("leaf_placement_attempts")
                .ForGetter<RandomSpreadFoliagePlacer, int>(p => p.LeafPlacementAttempts),
            (radius, offset, foliageHeight, leafPlacementAttempts) =>
                new RandomSpreadFoliagePlacer(radius, offset, foliageHeight, leafPlacementAttempts));

    public IntProvider FoliageHeightProvider { get; }
    public int LeafPlacementAttempts { get; }

    public RandomSpreadFoliagePlacer(IntProvider radius, IntProvider offset, IntProvider foliageHeight,
        int leafPlacementAttempts)
        : base(radius, offset)
    {
        FoliageHeightProvider = foliageHeight;
        LeafPlacementAttempts = leafPlacementAttempts;
    }

    public override FoliagePlacerType Type => FoliagePlacerTypes.RandomSpread;

    protected override void CreateFoliage(WorldGenRegion level, FoliageSetter foliageSetter, RandomSource random,
        TreeConfiguration config, int treeHeight, FoliageAttachment foliageAttachment, int foliageHeight,
        int leafRadius, int offset)
    {
        var origin = foliageAttachment.Pos;
        for (var i = 0; i < LeafPlacementAttempts; i++)
        {
            var pos = origin.Offset(
                random.NextInt(leafRadius) - random.NextInt(leafRadius),
                random.NextInt(foliageHeight) - random.NextInt(foliageHeight),
                random.NextInt(leafRadius) - random.NextInt(leafRadius));
            TryPlaceLeaf(level, foliageSetter, random, config, pos);
        }
    }

    public override int FoliageHeight(RandomSource random, int treeHeight, TreeConfiguration config)
        => FoliageHeightProvider.Sample(random);

    protected override bool ShouldSkipLocation(RandomSource random, int dx, int y, int dz, int currentRadius,
        bool doubleTrunk)
        => false;
}

//CherryFoliagePlacer 樱花树冠 带下垂树叶 对应原版 CherryFoliagePlacer
public sealed class CherryFoliagePlacer : FoliagePlacer
{
    public static readonly MapCodec<CherryFoliagePlacer> Codec =
        RecordCodecBuilder.Of7<CherryFoliagePlacer, IntProvider, IntProvider, IntProvider, float, float, float,
            float>(
            FoliagePlacerParts.Radius.ForGetter<CherryFoliagePlacer, IntProvider>(p => p.Radius),
            FoliagePlacerParts.Offset.ForGetter<CherryFoliagePlacer, IntProvider>(p => p.OffsetProvider),
            IntProviders.Codec.FieldOf("height").ForGetter<CherryFoliagePlacer, IntProvider>(p => p.Height),
            Codecs.Float.FieldOf("wide_bottom_layer_hole_chance")
                .ForGetter<CherryFoliagePlacer, float>(p => p.WideBottomLayerHoleChance),
            Codecs.Float.FieldOf("corner_hole_chance")
                .ForGetter<CherryFoliagePlacer, float>(p => p.CornerHoleChance),
            Codecs.Float.FieldOf("hanging_leaves_chance")
                .ForGetter<CherryFoliagePlacer, float>(p => p.HangingLeavesChance),
            Codecs.Float.FieldOf("hanging_leaves_extension_chance")
                .ForGetter<CherryFoliagePlacer, float>(p => p.HangingLeavesExtensionChance),
            (radius, offset, height, wideBottomLayerHoleChance, cornerHoleChance, hangingLeavesChance,
                    hangingLeavesExtensionChance) =>
                new CherryFoliagePlacer(radius, offset, height, wideBottomLayerHoleChance, cornerHoleChance,
                    hangingLeavesChance, hangingLeavesExtensionChance));

    public IntProvider Height { get; }
    public float WideBottomLayerHoleChance { get; }
    public float CornerHoleChance { get; }
    public float HangingLeavesChance { get; }
    public float HangingLeavesExtensionChance { get; }

    public CherryFoliagePlacer(IntProvider radius, IntProvider offset, IntProvider height,
        float wideBottomLayerHoleChance, float cornerHoleChance, float hangingLeavesChance,
        float hangingLeavesExtensionChance)
        : base(radius, offset)
    {
        Height = height;
        WideBottomLayerHoleChance = wideBottomLayerHoleChance;
        CornerHoleChance = cornerHoleChance;
        HangingLeavesChance = hangingLeavesChance;
        HangingLeavesExtensionChance = hangingLeavesExtensionChance;
    }

    public override FoliagePlacerType Type => FoliagePlacerTypes.Cherry;

    protected override void CreateFoliage(WorldGenRegion level, FoliageSetter foliageSetter, RandomSource random,
        TreeConfiguration config, int treeHeight, FoliageAttachment foliageAttachment, int foliageHeight,
        int leafRadius, int offset)
    {
        var doubleTrunk = foliageAttachment.DoubleTrunk;
        var foliagePos = foliageAttachment.Pos.Offset(0, offset, 0);
        var currentRadius = leafRadius + foliageAttachment.RadiusOffset - 1;
        PlaceLeavesRow(level, foliageSetter, random, config, foliagePos, currentRadius - 2, foliageHeight - 3,
            doubleTrunk);
        PlaceLeavesRow(level, foliageSetter, random, config, foliagePos, currentRadius - 1, foliageHeight - 4,
            doubleTrunk);
        for (var y = foliageHeight - 5; y >= 0; y--)
            PlaceLeavesRow(level, foliageSetter, random, config, foliagePos, currentRadius, y, doubleTrunk);
        PlaceLeavesRowWithHangingLeavesBelow(level, foliageSetter, random, config, foliagePos, currentRadius, -1,
            doubleTrunk, HangingLeavesChance, HangingLeavesExtensionChance);
        PlaceLeavesRowWithHangingLeavesBelow(level, foliageSetter, random, config, foliagePos, currentRadius - 1,
            -2, doubleTrunk, HangingLeavesChance, HangingLeavesExtensionChance);
    }

    public override int FoliageHeight(RandomSource random, int treeHeight, TreeConfiguration config)
        => Height.Sample(random);

    protected override bool ShouldSkipLocation(RandomSource random, int dx, int y, int dz, int currentRadius,
        bool doubleTrunk)
    {
        if (y == -1 && (dx == currentRadius || dz == currentRadius)
            && random.NextFloat() < WideBottomLayerHoleChance)
            return true;
        var corner = dx == currentRadius && dz == currentRadius;
        var wideLayer = currentRadius > 2;
        return wideLayer
            ? corner || (dx + dz > (currentRadius * 2) - 2 && random.NextFloat() < CornerHoleChance)
            : corner && random.NextFloat() < CornerHoleChance;
    }
}
