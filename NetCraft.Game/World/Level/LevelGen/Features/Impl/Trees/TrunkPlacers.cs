using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Util;
using NetCraft.Util.Random;
using PrimDirection = NetCraft.Primitives.Direction;
using RegBlock = NetCraft.Registry.Block;

namespace NetCraft.Game.World.Level.LevelGen.Features.Impl.Trees;

//TrunkPlacerType trunk placer type base, maps to vanilla TrunkPlacerType<P>
//Vanilla parameterizes the type; NetCraft generics are not covariant, so it is split into a non-generic base plus a generic middle layer
public abstract class TrunkPlacerType : NetCraft.Registry.TrunkPlacerType<object>
{
    public Identifier Id { get; }

    protected TrunkPlacerType(Identifier id) => Id = id;

    //Decode decode a placer instance from the map; the type field is already consumed by the caller
    public abstract DataResult<TrunkPlacer> Decode<U>(DynamicOps<U> ops, MapLike<U> input);

    //EncodeFields accumulate the instance fields into the builder; the type field is added by the caller
    public abstract void EncodeFields<U>(DynamicOps<U> ops, TrunkPlacer value, RecordBuilder<U> builder);
}

//TrunkPlacerType<P> generic middle layer for a concrete placer type; subclasses only supply one MapCodec<P>
public abstract class TrunkPlacerType<P> : TrunkPlacerType where P : TrunkPlacer
{
    private readonly MapCodec<P> _codec;

    protected TrunkPlacerType(Identifier id, MapCodec<P> codec) : base(id) => _codec = codec;

    public sealed override DataResult<TrunkPlacer> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => _codec.Decode(ops, input).Map(value => (TrunkPlacer)value);

    public sealed override void EncodeFields<U>(DynamicOps<U> ops, TrunkPlacer value, RecordBuilder<U> builder)
    {
        if (value is P typed) _codec.EncodeTo(ops, typed, builder);
    }
}

//SimpleTrunkPlacerType type instance carrying only an id and a codec, covering all built-in placers
internal sealed class SimpleTrunkPlacerType<P> : TrunkPlacerType<P> where P : TrunkPlacer
{
    public SimpleTrunkPlacerType(string id, MapCodec<P> codec)
        : base(Identifier.WithDefaultNamespace(id), codec) { }
}

//TrunkPlacerTypes built-in trunk placer type registration, maps to the static fields of vanilla TrunkPlacerType
public static class TrunkPlacerTypes
{
    public static readonly TrunkPlacerType<StraightTrunkPlacer> Straight =
        Register("straight_trunk_placer", StraightTrunkPlacer.Codec);

    public static readonly TrunkPlacerType<ForkingTrunkPlacer> Forking =
        Register("forking_trunk_placer", ForkingTrunkPlacer.Codec);

    public static readonly TrunkPlacerType<GiantTrunkPlacer> Giant =
        Register("giant_trunk_placer", GiantTrunkPlacer.Codec);

    public static readonly TrunkPlacerType<MegaJungleTrunkPlacer> MegaJungle =
        Register("mega_jungle_trunk_placer", MegaJungleTrunkPlacer.Codec);

    public static readonly TrunkPlacerType<DarkOakTrunkPlacer> DarkOak =
        Register("dark_oak_trunk_placer", DarkOakTrunkPlacer.Codec);

    public static readonly TrunkPlacerType<FancyTrunkPlacer> Fancy =
        Register("fancy_trunk_placer", FancyTrunkPlacer.Codec);

    public static readonly TrunkPlacerType<BendingTrunkPlacer> Bending =
        Register("bending_trunk_placer", BendingTrunkPlacer.Codec);

    public static readonly TrunkPlacerType<UpwardsBranchingTrunkPlacer> UpwardsBranching =
        Register("upwards_branching_trunk_placer", UpwardsBranchingTrunkPlacer.Codec);

    public static readonly TrunkPlacerType<CherryTrunkPlacer> Cherry =
        Register("cherry_trunk_placer", CherryTrunkPlacer.Codec);

    private static TrunkPlacerType<T> Register<T>(string path, MapCodec<T> codec) where T : TrunkPlacer
    {
        var type = new SimpleTrunkPlacerType<T>(path, codec);
        Registry<NetCraft.Registry.TrunkPlacerType<object>>.Register(
            BuiltInRegistries.TRUNK_PLACER_TYPE, type.Id, type);
        return type;
    }
}

//TrunkPlacerParts height fields shared by the three placers, maps to vanilla trunkPlacerParts
internal static class TrunkPlacerParts
{
    public static readonly MapCodec<int> BaseHeight = Codecs.Int.FieldOf("base_height");
    public static readonly MapCodec<int> HeightRandA = Codecs.Int.FieldOf("height_rand_a");
    public static readonly MapCodec<int> HeightRandB = Codecs.Int.FieldOf("height_rand_b");
}

//TrunkPlacer trunk placer base, maps to vanilla TrunkPlacer
public abstract class TrunkPlacer
{
    //Codec polymorphic entry dispatching on the type field into the TRUNK_PLACER_TYPE registry
    public static readonly Codec<TrunkPlacer> Codec = new TrunkPlacerDispatchCodec();

    public int BaseHeight { get; }
    public int HeightRandA { get; }
    public int HeightRandB { get; }

    protected TrunkPlacer(int baseHeight, int heightRandA, int heightRandB)
    {
        BaseHeight = baseHeight;
        HeightRandA = heightRandA;
        HeightRandB = heightRandB;
    }

    //Type owning type singleton; encoding and registry resolution use it to get the id
    public abstract TrunkPlacerType Type { get; }

    //GetTreeHeight roll the total tree height, maps to vanilla getTreeHeight
    public int GetTreeHeight(RandomSource random)
        => BaseHeight + random.NextInt(HeightRandA + 1) + random.NextInt(HeightRandB + 1);

    //PlaceTrunk grow the trunk and report foliage attachment points, maps to vanilla placeTrunk
    public abstract List<FoliagePlacer.FoliageAttachment> PlaceTrunk(WorldGenRegion level,
        Action<BlockPos, BlockState> trunkSetter, RandomSource random, int treeHeight, BlockPos origin,
        TreeConfiguration config);

    //PlaceBelowTrunkBlock fill the cell directly below the trunk from a provider, maps to vanilla placeBelowTrunkBlock
    protected static void PlaceBelowTrunkBlock(WorldGenRegion level, Action<BlockPos, BlockState> trunkSetter,
        RandomSource random, BlockPos pos, TreeConfiguration config)
    {
        var state = config.BelowTrunkProvider.GetOptionalState(level, random, pos);
        if (state is { } below) trunkSetter(pos, below);
    }

    //PlaceLog place one log, returning false when the position cannot be occupied, maps to vanilla placeLog
    protected bool PlaceLog(WorldGenRegion level, Action<BlockPos, BlockState> trunkSetter, RandomSource random,
        BlockPos pos, TreeConfiguration config, Func<BlockState, BlockState>? stateModifier = null)
    {
        if (!ValidTreePos(level, pos)) return false;
        var state = config.TrunkProvider.GetState(level, random, pos);
        if (stateModifier is not null) state = stateModifier(state);
        trunkSetter(pos, state);
        return true;
    }

    //PlaceLogIfFree place a log only when the position is free, maps to vanilla placeLogIfFree
    protected void PlaceLogIfFree(WorldGenRegion level, Action<BlockPos, BlockState> trunkSetter,
        RandomSource random, BlockPos pos, TreeConfiguration config)
    {
        if (IsFree(level, pos)) PlaceLog(level, trunkSetter, random, pos, config);
    }

    //ValidTreePos whether the position can be taken by a tree, maps to vanilla validTreePos
    protected virtual bool ValidTreePos(WorldGenRegion level, BlockPos pos) => TreeUtil.ValidTreePos(level, pos);

    //IsFree whether the position is free or already a log, maps to vanilla isFree
    public virtual bool IsFree(WorldGenRegion level, BlockPos pos)
        => ValidTreePos(level, pos) || TreeUtil.IsLogs(level, pos);
}

//TrunkPlacerDispatchCodec look up TRUNK_PLACER_TYPE by the type field then delegate to that type
internal sealed class TrunkPlacerDispatchCodec : ScalarCodec<TrunkPlacer>
{
    public override DataResult<TrunkPlacer> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => DecodePlacer(ops, map));

    private static DataResult<TrunkPlacer> DecodePlacer<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var typeTag = input.Get("type");
        if (!typeTag.IsPresent) return DataResult<TrunkPlacer>.Error(() => "trunk placer is missing the type field");
        var typeText = ops.GetStringValue(typeTag.Get());
        if (!typeText.Result().IsPresent)
            return DataResult<TrunkPlacer>.Error(() => "trunk placer type must be a string");
        var typeId = Identifier.TryParse(typeText.GetOrThrow());
        if (typeId is null)
            return DataResult<TrunkPlacer>.Error(() => $"invalid placer type: {typeText.GetOrThrow()}");
        if (BuiltInRegistries.TRUNK_PLACER_TYPE.GetValue(typeId.Value) is not TrunkPlacerType type)
            return DataResult<TrunkPlacer>.Error(() => $"unknown trunk placer type: {typeId}");
        return type.Decode(ops, input);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, TrunkPlacer value)
    {
        var builder = ops.MapBuilder();
        builder.Add("type", ops.CreateString(value.Type.Id.ToString()));
        value.Type.EncodeFields(ops, value, builder);
        return builder.Build(ops.Empty());
    }
}

//StraightTrunkPlacer straight trunk, maps to vanilla StraightTrunkPlacer
public sealed class StraightTrunkPlacer : TrunkPlacer
{
    public static readonly MapCodec<StraightTrunkPlacer> Codec =
        RecordCodecBuilder.Of3<StraightTrunkPlacer, int, int, int>(
            TrunkPlacerParts.BaseHeight.ForGetter<StraightTrunkPlacer, int>(p => p.BaseHeight),
            TrunkPlacerParts.HeightRandA.ForGetter<StraightTrunkPlacer, int>(p => p.HeightRandA),
            TrunkPlacerParts.HeightRandB.ForGetter<StraightTrunkPlacer, int>(p => p.HeightRandB),
            (baseHeight, a, b) => new StraightTrunkPlacer(baseHeight, a, b));

    public StraightTrunkPlacer(int baseHeight, int heightRandA, int heightRandB)
        : base(baseHeight, heightRandA, heightRandB) { }

    public override TrunkPlacerType Type => TrunkPlacerTypes.Straight;

    public override List<FoliagePlacer.FoliageAttachment> PlaceTrunk(WorldGenRegion level,
        Action<BlockPos, BlockState> trunkSetter, RandomSource random, int treeHeight, BlockPos origin,
        TreeConfiguration config)
    {
        PlaceBelowTrunkBlock(level, trunkSetter, random, origin.Offset(0, -1, 0), config);
        for (var y = 0; y < treeHeight; y++)
            PlaceLog(level, trunkSetter, random, origin.Offset(0, y, 0), config);
        return new List<FoliagePlacer.FoliageAttachment>
        {
            new(origin.Offset(0, treeHeight, 0), 0, false),
        };
    }
}

//ForkingTrunkPlacer forking trunk, maps to vanilla ForkingTrunkPlacer
public sealed class ForkingTrunkPlacer : TrunkPlacer
{
    public static readonly MapCodec<ForkingTrunkPlacer> Codec =
        RecordCodecBuilder.Of3<ForkingTrunkPlacer, int, int, int>(
            TrunkPlacerParts.BaseHeight.ForGetter<ForkingTrunkPlacer, int>(p => p.BaseHeight),
            TrunkPlacerParts.HeightRandA.ForGetter<ForkingTrunkPlacer, int>(p => p.HeightRandA),
            TrunkPlacerParts.HeightRandB.ForGetter<ForkingTrunkPlacer, int>(p => p.HeightRandB),
            (baseHeight, a, b) => new ForkingTrunkPlacer(baseHeight, a, b));

    public ForkingTrunkPlacer(int baseHeight, int heightRandA, int heightRandB)
        : base(baseHeight, heightRandA, heightRandB) { }

    public override TrunkPlacerType Type => TrunkPlacerTypes.Forking;

    public override List<FoliagePlacer.FoliageAttachment> PlaceTrunk(WorldGenRegion level,
        Action<BlockPos, BlockState> trunkSetter, RandomSource random, int treeHeight, BlockPos origin,
        TreeConfiguration config)
    {
        var attachments = new List<FoliagePlacer.FoliageAttachment>();
        PlaceBelowTrunkBlock(level, trunkSetter, random, origin.Offset(0, -1, 0), config);
        var leanDirection = TreeUtil.RandomHorizontalDirection(random);
        var leanHeight = treeHeight - random.NextInt(4) - 1;
        var leanSteps = 3 - random.NextInt(3);
        var tx = origin.X;
        var tz = origin.Z;
        var ey = int.MinValue;
        for (var yo = 0; yo < treeHeight; yo++)
        {
            var yy = origin.Y + yo;
            if (yo >= leanHeight && leanSteps > 0)
            {
                tx += leanDirection.StepX;
                tz += leanDirection.StepZ;
                leanSteps--;
            }
            if (PlaceLog(level, trunkSetter, random, new BlockPos(tx, yy, tz), config))
                ey = yy + 1;
        }
        if (ey != int.MinValue)
            attachments.Add(new FoliagePlacer.FoliageAttachment(new BlockPos(tx, ey, tz), 1, false));

        var bx = origin.X;
        var bz = origin.Z;
        var branchDirection = TreeUtil.RandomHorizontalDirection(random);
        if (branchDirection != leanDirection)
        {
            var branchPos = leanHeight - random.NextInt(2) - 1;
            var ey2 = int.MinValue;
            var yo2 = branchPos;
            for (var branchSteps = 1 + random.NextInt(3); yo2 < treeHeight && branchSteps > 0; branchSteps--)
            {
                if (yo2 >= 1)
                {
                    var yy2 = origin.Y + yo2;
                    bx += branchDirection.StepX;
                    bz += branchDirection.StepZ;
                    if (PlaceLog(level, trunkSetter, random, new BlockPos(bx, yy2, bz), config))
                        ey2 = yy2 + 1;
                }
                yo2++;
            }
            if (ey2 != int.MinValue)
                attachments.Add(new FoliagePlacer.FoliageAttachment(new BlockPos(bx, ey2, bz), 0, false));
        }
        return attachments;
    }
}

//GiantTrunkPlacer giant 2x2 trunk, maps to vanilla GiantTrunkPlacer
public class GiantTrunkPlacer : TrunkPlacer
{
    public static readonly MapCodec<GiantTrunkPlacer> Codec =
        RecordCodecBuilder.Of3<GiantTrunkPlacer, int, int, int>(
            TrunkPlacerParts.BaseHeight.ForGetter<GiantTrunkPlacer, int>(p => p.BaseHeight),
            TrunkPlacerParts.HeightRandA.ForGetter<GiantTrunkPlacer, int>(p => p.HeightRandA),
            TrunkPlacerParts.HeightRandB.ForGetter<GiantTrunkPlacer, int>(p => p.HeightRandB),
            (baseHeight, a, b) => new GiantTrunkPlacer(baseHeight, a, b));

    public GiantTrunkPlacer(int baseHeight, int heightRandA, int heightRandB)
        : base(baseHeight, heightRandA, heightRandB) { }

    public override TrunkPlacerType Type => TrunkPlacerTypes.Giant;

    public override List<FoliagePlacer.FoliageAttachment> PlaceTrunk(WorldGenRegion level,
        Action<BlockPos, BlockState> trunkSetter, RandomSource random, int treeHeight, BlockPos origin,
        TreeConfiguration config)
    {
        var below = origin.Offset(0, -1, 0);
        PlaceBelowTrunkBlock(level, trunkSetter, random, below, config);
        PlaceBelowTrunkBlock(level, trunkSetter, random, below.Offset(1, 0, 0), config);
        PlaceBelowTrunkBlock(level, trunkSetter, random, below.Offset(0, 0, 1), config);
        PlaceBelowTrunkBlock(level, trunkSetter, random, below.Offset(1, 0, 1), config);
        for (var hh = 0; hh < treeHeight; hh++)
        {
            PlaceLogIfFreeWithOffset(level, trunkSetter, random, config, origin, 0, hh, 0);
            if (hh < treeHeight - 1)
            {
                PlaceLogIfFreeWithOffset(level, trunkSetter, random, config, origin, 1, hh, 0);
                PlaceLogIfFreeWithOffset(level, trunkSetter, random, config, origin, 1, hh, 1);
                PlaceLogIfFreeWithOffset(level, trunkSetter, random, config, origin, 0, hh, 1);
            }
        }
        return new List<FoliagePlacer.FoliageAttachment>
        {
            new(origin.Offset(0, treeHeight, 0), 0, true),
        };
    }

    private void PlaceLogIfFreeWithOffset(WorldGenRegion level, Action<BlockPos, BlockState> trunkSetter,
        RandomSource random, TreeConfiguration config, BlockPos treePos, int x, int y, int z)
        => PlaceLogIfFree(level, trunkSetter, random, treePos.Offset(x, y, z), config);
}

//MegaJungleTrunkPlacer mega jungle trunk adding side branches on a giant trunk, maps to vanilla MegaJungleTrunkPlacer
public sealed class MegaJungleTrunkPlacer : GiantTrunkPlacer
{
    public static readonly MapCodec<MegaJungleTrunkPlacer> Codec =
        RecordCodecBuilder.Of3<MegaJungleTrunkPlacer, int, int, int>(
            TrunkPlacerParts.BaseHeight.ForGetter<MegaJungleTrunkPlacer, int>(p => p.BaseHeight),
            TrunkPlacerParts.HeightRandA.ForGetter<MegaJungleTrunkPlacer, int>(p => p.HeightRandA),
            TrunkPlacerParts.HeightRandB.ForGetter<MegaJungleTrunkPlacer, int>(p => p.HeightRandB),
            (baseHeight, a, b) => new MegaJungleTrunkPlacer(baseHeight, a, b));

    public MegaJungleTrunkPlacer(int baseHeight, int heightRandA, int heightRandB)
        : base(baseHeight, heightRandA, heightRandB) { }

    public override TrunkPlacerType Type => TrunkPlacerTypes.MegaJungle;

    public override List<FoliagePlacer.FoliageAttachment> PlaceTrunk(WorldGenRegion level,
        Action<BlockPos, BlockState> trunkSetter, RandomSource random, int treeHeight, BlockPos origin,
        TreeConfiguration config)
    {
        var attachments = new List<FoliagePlacer.FoliageAttachment>();
        attachments.AddRange(base.PlaceTrunk(level, trunkSetter, random, treeHeight, origin, config));
        var i = treeHeight - 2;
        var iNextInt = random.NextInt(4);
        while (true)
        {
            var branchHeight = i - iNextInt;
            if (branchHeight <= treeHeight / 2) return attachments;
            var angle = random.NextFloat() * Mth.TwoPi;
            var bx = 0;
            var bz = 0;
            for (var b = 0; b < 5; b++)
            {
                bx = (int)(1.5f + (Mth.Cos(angle) * b));
                bz = (int)(1.5f + (Mth.Sin(angle) * b));
                var pos = origin.Offset(bx, branchHeight - 3 + (b / 2), bz);
                PlaceLog(level, trunkSetter, random, pos, config);
            }
            attachments.Add(new FoliagePlacer.FoliageAttachment(origin.Offset(bx, branchHeight, bz), -2, false));
            i = branchHeight;
            iNextInt = 2 + random.NextInt(4);
        }
    }
}

//DarkOakTrunkPlacer dark oak 2x2 trunk with lean and side branches, maps to vanilla DarkOakTrunkPlacer
public sealed class DarkOakTrunkPlacer : TrunkPlacer
{
    public static readonly MapCodec<DarkOakTrunkPlacer> Codec =
        RecordCodecBuilder.Of3<DarkOakTrunkPlacer, int, int, int>(
            TrunkPlacerParts.BaseHeight.ForGetter<DarkOakTrunkPlacer, int>(p => p.BaseHeight),
            TrunkPlacerParts.HeightRandA.ForGetter<DarkOakTrunkPlacer, int>(p => p.HeightRandA),
            TrunkPlacerParts.HeightRandB.ForGetter<DarkOakTrunkPlacer, int>(p => p.HeightRandB),
            (baseHeight, a, b) => new DarkOakTrunkPlacer(baseHeight, a, b));

    public DarkOakTrunkPlacer(int baseHeight, int heightRandA, int heightRandB)
        : base(baseHeight, heightRandA, heightRandB) { }

    public override TrunkPlacerType Type => TrunkPlacerTypes.DarkOak;

    public override List<FoliagePlacer.FoliageAttachment> PlaceTrunk(WorldGenRegion level,
        Action<BlockPos, BlockState> trunkSetter, RandomSource random, int treeHeight, BlockPos origin,
        TreeConfiguration config)
    {
        var attachments = new List<FoliagePlacer.FoliageAttachment>();
        var below = origin.Offset(0, -1, 0);
        PlaceBelowTrunkBlock(level, trunkSetter, random, below, config);
        PlaceBelowTrunkBlock(level, trunkSetter, random, below.Offset(1, 0, 0), config);
        PlaceBelowTrunkBlock(level, trunkSetter, random, below.Offset(0, 0, 1), config);
        PlaceBelowTrunkBlock(level, trunkSetter, random, below.Offset(1, 0, 1), config);
        var leanDirection = TreeUtil.RandomHorizontalDirection(random);
        var leanHeight = treeHeight - random.NextInt(4);
        var leanSteps = 2 - random.NextInt(3);
        var x = origin.X;
        var y = origin.Y;
        var z = origin.Z;
        var tx = x;
        var tz = z;
        var ey = y + treeHeight - 1;
        for (var dy = 0; dy < treeHeight; dy++)
        {
            if (dy >= leanHeight && leanSteps > 0)
            {
                tx += leanDirection.StepX;
                tz += leanDirection.StepZ;
                leanSteps--;
            }
            var blockPos = new BlockPos(tx, y + dy, tz);
            if (!TreeUtil.IsAirOrLeaves(level, blockPos)) continue;
            PlaceLog(level, trunkSetter, random, blockPos, config);
            PlaceLog(level, trunkSetter, random, blockPos.Offset(1, 0, 0), config);
            PlaceLog(level, trunkSetter, random, blockPos.Offset(0, 0, 1), config);
            PlaceLog(level, trunkSetter, random, blockPos.Offset(1, 0, 1), config);
        }
        attachments.Add(new FoliagePlacer.FoliageAttachment(new BlockPos(tx, ey, tz), 0, true));
        for (var ox = -1; ox <= 2; ox++)
        {
            for (var oz = -1; oz <= 2; oz++)
            {
                if ((ox < 0 || ox > 1 || oz < 0 || oz > 1) && random.NextInt(3) <= 0)
                {
                    var length = random.NextInt(3) + 2;
                    for (var branchY = 0; branchY < length; branchY++)
                        PlaceLog(level, trunkSetter, random, new BlockPos(x + ox, ey - branchY - 1, z + oz),
                            config);
                    attachments.Add(new FoliagePlacer.FoliageAttachment(new BlockPos(x + ox, ey, z + oz), 0,
                        false));
                }
            }
        }
        return attachments;
    }
}

//FancyTrunkPlacer fancy oak: a tall thin trunk with spherical branch clusters, maps to vanilla FancyTrunkPlacer
public sealed class FancyTrunkPlacer : TrunkPlacer
{
    public static readonly MapCodec<FancyTrunkPlacer> Codec =
        RecordCodecBuilder.Of3<FancyTrunkPlacer, int, int, int>(
            TrunkPlacerParts.BaseHeight.ForGetter<FancyTrunkPlacer, int>(p => p.BaseHeight),
            TrunkPlacerParts.HeightRandA.ForGetter<FancyTrunkPlacer, int>(p => p.HeightRandA),
            TrunkPlacerParts.HeightRandB.ForGetter<FancyTrunkPlacer, int>(p => p.HeightRandB),
            (baseHeight, a, b) => new FancyTrunkPlacer(baseHeight, a, b));

    private const double TrunkHeightScale = 0.618d;
    private const double ClusterDensityMagic = 1.382d;
    private const double BranchSlope = 0.381d;
    private const double BranchLengthMagic = 0.328d;

    public FancyTrunkPlacer(int baseHeight, int heightRandA, int heightRandB)
        : base(baseHeight, heightRandA, heightRandB) { }

    public override TrunkPlacerType Type => TrunkPlacerTypes.Fancy;

    public override List<FoliagePlacer.FoliageAttachment> PlaceTrunk(WorldGenRegion level,
        Action<BlockPos, BlockState> trunkSetter, RandomSource random, int treeHeight, BlockPos origin,
        TreeConfiguration config)
    {
        var height = treeHeight + 2;
        var trunkHeight = Mth.Floor(height * TrunkHeightScale);
        PlaceBelowTrunkBlock(level, trunkSetter, random, origin.Offset(0, -1, 0), config);
        var clustersPerY = Math.Min(1, Mth.Floor(ClusterDensityMagic + Math.Pow(height / 13.0d, 2.0d)));
        var trunkTop = origin.Y + trunkHeight;
        var relativeY = height - 5;
        var foliageCoords = new List<FoliageCoords>
        {
            new(origin.Offset(0, relativeY, 0), trunkTop),
        };
        while (relativeY >= 0)
        {
            var treeShape = TreeShape(height, relativeY);
            if (treeShape >= 0.0f)
            {
                for (var i = 0; i < clustersPerY; i++)
                {
                    var radius = 1.0d * treeShape * (random.NextFloat() + BranchLengthMagic);
                    var angle = random.NextFloat() * 2.0f * Math.PI;
                    var x = (radius * Math.Sin(angle)) + 0.5d;
                    var z = (radius * Math.Cos(angle)) + 0.5d;
                    var checkStart = origin.Offset(Mth.Floor(x), relativeY - 1, Mth.Floor(z));
                    var checkEnd = checkStart.Offset(0, 5, 0);
                    if (MakeLimb(level, trunkSetter, random, checkStart, checkEnd, false, config))
                    {
                        var dx = origin.X - checkStart.X;
                        var dz = origin.Z - checkStart.Z;
                        var branchHeight = checkStart.Y - (Math.Sqrt((dx * dx) + (dz * dz)) * BranchSlope);
                        var branchTop = branchHeight > trunkTop ? trunkTop : (int)branchHeight;
                        var checkBranchBase = new BlockPos(origin.X, branchTop, origin.Z);
                        if (MakeLimb(level, trunkSetter, random, checkBranchBase, checkStart, false, config))
                            foliageCoords.Add(new FoliageCoords(checkStart, checkBranchBase.Y));
                    }
                }
            }
            relativeY--;
        }
        MakeLimb(level, trunkSetter, random, origin, origin.Offset(0, trunkHeight, 0), true, config);
        MakeBranches(level, trunkSetter, random, height, origin, foliageCoords, config);
        var attachments = new List<FoliagePlacer.FoliageAttachment>();
        foreach (var coord in foliageCoords)
            if (TrimBranches(height, coord.BranchBase - origin.Y))
                attachments.Add(coord.Attachment);
        return attachments;
    }

    //MakeLimb place a line of logs between two points or only check placement, maps to vanilla makeLimb
    private bool MakeLimb(WorldGenRegion level, Action<BlockPos, BlockState> trunkSetter, RandomSource random,
        BlockPos startPos, BlockPos endPos, bool doPlace, TreeConfiguration config)
    {
        if (!doPlace && startPos == endPos) return true;
        var delta = endPos.Offset(-startPos.X, -startPos.Y, -startPos.Z);
        var steps = GetSteps(delta);
        var dx = (float)delta.X / steps;
        var dy = (float)delta.Y / steps;
        var dz = (float)delta.Z / steps;
        for (var i = 0; i <= steps; i++)
        {
            var pos = startPos.Offset(Mth.Floor(0.5f + (i * dx)), Mth.Floor(0.5f + (i * dy)),
                Mth.Floor(0.5f + (i * dz)));
            if (doPlace)
            {
                PlaceLog(level, trunkSetter, random, pos, config,
                    state => state.TrySetValue(BlockStateProperties.AxisProperty,
                        TreeUtil.ToEnumAxis(GetLogAxis(startPos, pos))));
            }
            else if (!IsFree(level, pos))
            {
                return false;
            }
        }
        return true;
    }

    private static int GetSteps(BlockPos pos)
        => Math.Max(Math.Abs(pos.X), Math.Max(Math.Abs(pos.Y), Math.Abs(pos.Z)));

    private static PrimDirection.Axis GetLogAxis(BlockPos startPos, BlockPos blockPos)
    {
        var axis = PrimDirection.Axis.Y;
        var xdiff = Math.Abs(blockPos.X - startPos.X);
        var zdiff = Math.Abs(blockPos.Z - startPos.Z);
        var maxdiff = Math.Max(xdiff, zdiff);
        if (maxdiff > 0) axis = xdiff == maxdiff ? PrimDirection.Axis.X : PrimDirection.Axis.Z;
        return axis;
    }

    private static bool TrimBranches(int height, int localY) => localY >= height * 0.2d;

    private void MakeBranches(WorldGenRegion level, Action<BlockPos, BlockState> trunkSetter, RandomSource random,
        int height, BlockPos origin, List<FoliageCoords> foliageCoords, TreeConfiguration config)
    {
        foreach (var endCoord in foliageCoords)
        {
            var branchBase = endCoord.BranchBase;
            var baseCoord = new BlockPos(origin.X, branchBase, origin.Z);
            if (baseCoord != endCoord.Attachment.Pos && TrimBranches(height, branchBase - origin.Y))
                MakeLimb(level, trunkSetter, random, baseCoord, endCoord.Attachment.Pos, true, config);
        }
    }

    //TreeShape canopy radius at this height, maps to vanilla treeShape
    private static float TreeShape(int height, int y)
    {
        if (y < height * 0.3f) return -1.0f;
        var radius = height / 2.0f;
        var adjacent = radius - y;
        var distance = Mth.Sqrt((radius * radius) - (adjacent * adjacent));
        if (adjacent == 0.0f) return radius;
        if (Math.Abs(adjacent) >= radius) return 0.0f;
        return distance * 0.5f;
    }

    private sealed class FoliageCoords
    {
        public FoliagePlacer.FoliageAttachment Attachment { get; }
        public int BranchBase { get; }

        public FoliageCoords(BlockPos pos, int branchBase)
        {
            Attachment = new FoliagePlacer.FoliageAttachment(pos, 0, false);
            BranchBase = branchBase;
        }
    }
}

//BendingTrunkPlacer bending trunk used by lilac and azalea, maps to vanilla BendingTrunkPlacer
public sealed class BendingTrunkPlacer : TrunkPlacer
{
    public static readonly MapCodec<BendingTrunkPlacer> Codec =
        RecordCodecBuilder.Of5<BendingTrunkPlacer, int, int, int, int, IntProvider>(
            TrunkPlacerParts.BaseHeight.ForGetter<BendingTrunkPlacer, int>(p => p.BaseHeight),
            TrunkPlacerParts.HeightRandA.ForGetter<BendingTrunkPlacer, int>(p => p.HeightRandA),
            TrunkPlacerParts.HeightRandB.ForGetter<BendingTrunkPlacer, int>(p => p.HeightRandB),
            Codecs.Int.FieldOf("min_height_for_leaves")
                .ForGetter<BendingTrunkPlacer, int>(p => p.MinHeightForLeaves),
            IntProviders.Codec.FieldOf("bend_length")
                .ForGetter<BendingTrunkPlacer, IntProvider>(p => p.BendLength),
            (baseHeight, a, b, minHeightForLeaves, bendLength) =>
                new BendingTrunkPlacer(baseHeight, a, b, minHeightForLeaves, bendLength));

    public int MinHeightForLeaves { get; }
    public IntProvider BendLength { get; }

    public BendingTrunkPlacer(int baseHeight, int heightRandA, int heightRandB, int minHeightForLeaves,
        IntProvider bendLength)
        : base(baseHeight, heightRandA, heightRandB)
    {
        MinHeightForLeaves = minHeightForLeaves;
        BendLength = bendLength;
    }

    public override TrunkPlacerType Type => TrunkPlacerTypes.Bending;

    public override List<FoliagePlacer.FoliageAttachment> PlaceTrunk(WorldGenRegion level,
        Action<BlockPos, BlockState> trunkSetter, RandomSource random, int treeHeight, BlockPos origin,
        TreeConfiguration config)
    {
        var direction = TreeUtil.RandomHorizontalDirection(random);
        var logHeight = treeHeight - 1;
        var pos = origin;
        PlaceBelowTrunkBlock(level, trunkSetter, random, pos.Offset(0, -1, 0), config);
        var foliagePoints = new List<FoliagePlacer.FoliageAttachment>();
        for (var i = 0; i <= logHeight; i++)
        {
            if (i + 1 >= logHeight + random.NextInt(2)) pos = pos.Offset(direction);
            if (TreeUtil.ValidTreePos(level, pos)) PlaceLog(level, trunkSetter, random, pos, config);
            if (i >= MinHeightForLeaves) foliagePoints.Add(new FoliagePlacer.FoliageAttachment(pos, 0, false));
            pos = pos.Offset(PrimDirection.Up);
        }
        var dirLength = BendLength.Sample(random);
        for (var i = 0; i <= dirLength; i++)
        {
            if (TreeUtil.ValidTreePos(level, pos)) PlaceLog(level, trunkSetter, random, pos, config);
            foliagePoints.Add(new FoliagePlacer.FoliageAttachment(pos, 0, false));
            pos = pos.Offset(direction);
        }
        return foliagePoints;
    }
}

//UpwardsBranchingTrunkPlacer upward-branching trunk for mangrove, maps to vanilla UpwardsBranchingTrunkPlacer
public sealed class UpwardsBranchingTrunkPlacer : TrunkPlacer
{
    public static readonly MapCodec<UpwardsBranchingTrunkPlacer> Codec =
        RecordCodecBuilder.Of7<UpwardsBranchingTrunkPlacer, int, int, int, IntProvider, float, IntProvider,
            HolderSet<RegBlock>>(
            TrunkPlacerParts.BaseHeight.ForGetter<UpwardsBranchingTrunkPlacer, int>(p => p.BaseHeight),
            TrunkPlacerParts.HeightRandA.ForGetter<UpwardsBranchingTrunkPlacer, int>(p => p.HeightRandA),
            TrunkPlacerParts.HeightRandB.ForGetter<UpwardsBranchingTrunkPlacer, int>(p => p.HeightRandB),
            IntProviders.PositiveCodec.FieldOf("extra_branch_steps")
                .ForGetter<UpwardsBranchingTrunkPlacer, IntProvider>(p => p.ExtraBranchSteps),
            Codecs.Float.FieldOf("place_branch_per_log_probability")
                .ForGetter<UpwardsBranchingTrunkPlacer, float>(p => p.PlaceBranchPerLogProbability),
            IntProviders.NonNegativeCodec.FieldOf("extra_branch_length")
                .ForGetter<UpwardsBranchingTrunkPlacer, IntProvider>(p => p.ExtraBranchLength),
            HolderSetCodecs.BlockSet.FieldOf("can_grow_through")
                .ForGetter<UpwardsBranchingTrunkPlacer, HolderSet<RegBlock>>(p => p.CanGrowThrough),
            (baseHeight, a, b, extraBranchSteps, placeBranchPerLogProbability, extraBranchLength,
                    canGrowThrough) =>
                new UpwardsBranchingTrunkPlacer(baseHeight, a, b, extraBranchSteps,
                    placeBranchPerLogProbability, extraBranchLength, canGrowThrough));

    public IntProvider ExtraBranchSteps { get; }
    public float PlaceBranchPerLogProbability { get; }
    public IntProvider ExtraBranchLength { get; }
    public HolderSet<RegBlock> CanGrowThrough { get; }

    public UpwardsBranchingTrunkPlacer(int baseHeight, int heightRandA, int heightRandB,
        IntProvider extraBranchSteps, float placeBranchPerLogProbability, IntProvider extraBranchLength,
        HolderSet<RegBlock> canGrowThrough)
        : base(baseHeight, heightRandA, heightRandB)
    {
        ExtraBranchSteps = extraBranchSteps;
        PlaceBranchPerLogProbability = placeBranchPerLogProbability;
        ExtraBranchLength = extraBranchLength;
        CanGrowThrough = canGrowThrough;
    }

    public override TrunkPlacerType Type => TrunkPlacerTypes.UpwardsBranching;

    public override List<FoliagePlacer.FoliageAttachment> PlaceTrunk(WorldGenRegion level,
        Action<BlockPos, BlockState> trunkSetter, RandomSource random, int treeHeight, BlockPos origin,
        TreeConfiguration config)
    {
        var attachments = new List<FoliagePlacer.FoliageAttachment>();
        for (var heightPos = 0; heightPos < treeHeight; heightPos++)
        {
            var currentHeight = origin.Y + heightPos;
            var logPos = new BlockPos(origin.X, currentHeight, origin.Z);
            if (PlaceLog(level, trunkSetter, random, logPos, config) && heightPos < treeHeight - 1
                && random.NextFloat() < PlaceBranchPerLogProbability)
            {
                var branchDir = TreeUtil.RandomHorizontalDirection(random);
                var branchLen = ExtraBranchLength.Sample(random);
                var branchPos = Math.Max(0, branchLen - ExtraBranchLength.Sample(random) - 1);
                var branchSteps = ExtraBranchSteps.Sample(random);
                PlaceBranch(level, trunkSetter, random, treeHeight, config, attachments, logPos, currentHeight,
                    branchDir, branchPos, branchSteps);
            }
            if (heightPos == treeHeight - 1)
                attachments.Add(new FoliagePlacer.FoliageAttachment(
                    new BlockPos(origin.X, currentHeight + 1, origin.Z), 0, false));
        }
        return attachments;
    }

    private void PlaceBranch(WorldGenRegion level, Action<BlockPos, BlockState> trunkSetter, RandomSource random,
        int treeHeight, TreeConfiguration config, List<FoliagePlacer.FoliageAttachment> attachments,
        BlockPos logPos, int currentHeight, PrimDirection branchDir, int branchPos, int branchSteps)
    {
        var heightAlongBranch = currentHeight + branchPos;
        var logX = logPos.X;
        var logZ = logPos.Z;
        var branchPlacementIndex = branchPos;
        while (branchPlacementIndex < treeHeight && branchSteps > 0)
        {
            if (branchPlacementIndex >= 1)
            {
                var placementHeight = currentHeight + branchPlacementIndex;
                logX += branchDir.StepX;
                logZ += branchDir.StepZ;
                heightAlongBranch = placementHeight;
                if (PlaceLog(level, trunkSetter, random, new BlockPos(logX, placementHeight, logZ), config))
                    heightAlongBranch++;
                attachments.Add(new FoliagePlacer.FoliageAttachment(
                    new BlockPos(logX, placementHeight, logZ), 0, false));
            }
            branchPlacementIndex++;
            branchSteps--;
        }
        if (heightAlongBranch - currentHeight > 1)
        {
            var foliagePos = new BlockPos(logX, heightAlongBranch, logZ);
            attachments.Add(new FoliagePlacer.FoliageAttachment(foliagePos, 0, false));
            attachments.Add(new FoliagePlacer.FoliageAttachment(foliagePos.Offset(0, -2, 0), 0, false));
        }
    }

    //ValidTreePos allows passing through a given block set, maps to the vanilla validTreePos override
    protected override bool ValidTreePos(WorldGenRegion level, BlockPos pos)
        => base.ValidTreePos(level, pos)
           || CanGrowThrough.Contains(BuiltInRegistries.BLOCK.WrapAsHolder(level.GetBlockState(pos.X, pos.Y, pos.Z).Owner));
}

//UniformIntObjectCodec uniform int codec without a type field
//Maps to vanilla UniformInt.MAP_CODEC.codec(); CherryTrunkPlacer's branch_start_offset_from_top uses it
internal sealed class UniformIntObjectCodec : ScalarCodec<UniformInt>
{
    public static readonly UniformIntObjectCodec Instance = new();

    public override DataResult<UniformInt> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map =>
        {
            var minTag = map.Get("min_inclusive");
            var maxTag = map.Get("max_inclusive");
            if (!minTag.IsPresent || !maxTag.IsPresent)
                return DataResult<UniformInt>.Error(() => "uniform distribution requires min_inclusive and max_inclusive");
            var min = ops.GetNumberValue(minTag.Get());
            var max = ops.GetNumberValue(maxTag.Get());
            if (!min.Result().IsPresent || !max.Result().IsPresent)
                return DataResult<UniformInt>.Error(() => "uniform bounds must be numbers");
            var minValue = (int)min.GetOrThrow();
            var maxValue = (int)max.GetOrThrow();
            if (maxValue < minValue)
                return DataResult<UniformInt>.Error(() => $"upper bound must not be less than the lower bound [{minValue}-{maxValue}]");
            return DataResult<UniformInt>.Success(new UniformInt(minValue, maxValue));
        });

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, UniformInt value)
    {
        var builder = ops.MapBuilder();
        builder.Add("min_inclusive", ops.CreateInt(value.MinInclusive));
        builder.Add("max_inclusive", ops.CreateInt(value.MaxInclusive));
        return builder.Build(ops.Empty());
    }
}

//CherryTrunkPlacer cherry trunk: a main trunk with one or two side branches, maps to vanilla CherryTrunkPlacer
public sealed class CherryTrunkPlacer : TrunkPlacer
{
    public static readonly MapCodec<CherryTrunkPlacer> Codec =
        RecordCodecBuilder.Of7<CherryTrunkPlacer, int, int, int, IntProvider, IntProvider, UniformInt,
            IntProvider>(
            TrunkPlacerParts.BaseHeight.ForGetter<CherryTrunkPlacer, int>(p => p.BaseHeight),
            TrunkPlacerParts.HeightRandA.ForGetter<CherryTrunkPlacer, int>(p => p.HeightRandA),
            TrunkPlacerParts.HeightRandB.ForGetter<CherryTrunkPlacer, int>(p => p.HeightRandB),
            IntProviders.Codec.FieldOf("branch_count")
                .ForGetter<CherryTrunkPlacer, IntProvider>(p => p.BranchCount),
            IntProviders.Codec.FieldOf("branch_horizontal_length")
                .ForGetter<CherryTrunkPlacer, IntProvider>(p => p.BranchHorizontalLength),
            UniformIntObjectCodec.Instance.FieldOf("branch_start_offset_from_top")
                .ForGetter<CherryTrunkPlacer, UniformInt>(p => p.BranchStartOffsetFromTop),
            IntProviders.Codec.FieldOf("branch_end_offset_from_top")
                .ForGetter<CherryTrunkPlacer, IntProvider>(p => p.BranchEndOffsetFromTop),
            (baseHeight, a, b, branchCount, branchHorizontalLength, branchStartOffsetFromTop,
                    branchEndOffsetFromTop) =>
                new CherryTrunkPlacer(baseHeight, a, b, branchCount, branchHorizontalLength,
                    branchStartOffsetFromTop, branchEndOffsetFromTop));

    public IntProvider BranchCount { get; }
    public IntProvider BranchHorizontalLength { get; }
    public UniformInt BranchStartOffsetFromTop { get; }
    public UniformInt SecondBranchStartOffsetFromTop { get; }
    public IntProvider BranchEndOffsetFromTop { get; }

    public CherryTrunkPlacer(int baseHeight, int heightRandA, int heightRandB, IntProvider branchCount,
        IntProvider branchHorizontalLength, UniformInt branchStartOffsetFromTop, IntProvider branchEndOffsetFromTop)
        : base(baseHeight, heightRandA, heightRandB)
    {
        BranchCount = branchCount;
        BranchHorizontalLength = branchHorizontalLength;
        BranchStartOffsetFromTop = branchStartOffsetFromTop;
        SecondBranchStartOffsetFromTop = UniformInt.Of(branchStartOffsetFromTop.MinInclusive,
            branchStartOffsetFromTop.MaxInclusive - 1);
        BranchEndOffsetFromTop = branchEndOffsetFromTop;
    }

    public override TrunkPlacerType Type => TrunkPlacerTypes.Cherry;

    public override List<FoliagePlacer.FoliageAttachment> PlaceTrunk(WorldGenRegion level,
        Action<BlockPos, BlockState> trunkSetter, RandomSource random, int treeHeight, BlockPos origin,
        TreeConfiguration config)
    {
        PlaceBelowTrunkBlock(level, trunkSetter, random, origin.Offset(0, -1, 0), config);
        var firstBranchOffsetFromOrigin = Math.Max(0, treeHeight - 1 + BranchStartOffsetFromTop.Sample(random));
        var secondBranchOffsetFromOrigin =
            Math.Max(0, treeHeight - 1 + SecondBranchStartOffsetFromTop.Sample(random));
        if (secondBranchOffsetFromOrigin >= firstBranchOffsetFromOrigin) secondBranchOffsetFromOrigin++;
        var branchCount = BranchCount.Sample(random);
        var hasMiddleBranch = branchCount == 3;
        var hasBothSideBranches = branchCount >= 2;
        int trunkHeight;
        if (hasMiddleBranch) trunkHeight = treeHeight;
        else if (hasBothSideBranches)
            trunkHeight = Math.Max(firstBranchOffsetFromOrigin, secondBranchOffsetFromOrigin) + 1;
        else trunkHeight = firstBranchOffsetFromOrigin + 1;
        for (var y = 0; y < trunkHeight; y++)
            PlaceLog(level, trunkSetter, random, origin.Offset(0, y, 0), config);
        var attachments = new List<FoliagePlacer.FoliageAttachment>();
        if (hasMiddleBranch)
            attachments.Add(new FoliagePlacer.FoliageAttachment(origin.Offset(0, trunkHeight, 0), 0, false));
        var treeDirection = TreeUtil.RandomHorizontalDirection(random);
        Func<BlockState, BlockState> sidewaysStateModifier = state =>
            state.TrySetValue(BlockStateProperties.AxisProperty,
                TreeUtil.ToEnumAxis(treeDirection.AxisValue));
        attachments.Add(GenerateBranch(level, trunkSetter, random, treeHeight, origin, config,
            sidewaysStateModifier, treeDirection, firstBranchOffsetFromOrigin,
            firstBranchOffsetFromOrigin < trunkHeight - 1));
        if (hasBothSideBranches)
            attachments.Add(GenerateBranch(level, trunkSetter, random, treeHeight, origin, config,
                sidewaysStateModifier, treeDirection.Opposite, secondBranchOffsetFromOrigin,
                secondBranchOffsetFromOrigin < trunkHeight - 1));
        return attachments;
    }

    //GenerateBranch grow one branch from the trunk to one side, maps to vanilla generateBranch
    private FoliagePlacer.FoliageAttachment GenerateBranch(WorldGenRegion level,
        Action<BlockPos, BlockState> trunkSetter, RandomSource random, int treeHeight, BlockPos origin,
        TreeConfiguration config, Func<BlockState, BlockState> sidewaysStateModifier,
        PrimDirection branchDirection, int offsetFromOrigin, bool middleContinuesUpwards)
    {
        var logPos = origin.Offset(0, offsetFromOrigin, 0);
        var branchEndPosOffsetFromOrigin = treeHeight - 1 + BranchEndOffsetFromTop.Sample(random);
        var extendBranchAwayFromTrunk = middleContinuesUpwards || branchEndPosOffsetFromOrigin < offsetFromOrigin;
        var distanceToTrunk = BranchHorizontalLength.Sample(random) + (extendBranchAwayFromTrunk ? 1 : 0);
        var branchEndPos = origin.Offset(branchDirection.StepX * distanceToTrunk,
            branchEndPosOffsetFromOrigin, branchDirection.StepZ * distanceToTrunk);
        var stepsHorizontally = extendBranchAwayFromTrunk ? 2 : 1;
        for (var i = 0; i < stepsHorizontally; i++)
        {
            logPos = logPos.Offset(branchDirection);
            PlaceLog(level, trunkSetter, random, logPos, config, sidewaysStateModifier);
        }
        var verticalDirection = branchEndPos.Y > logPos.Y ? PrimDirection.Up : PrimDirection.Down;
        while (true)
        {
            var distance = TreeUtil.DistManhattan(logPos, branchEndPos);
            if (distance == 0) return new FoliagePlacer.FoliageAttachment(branchEndPos.Offset(0, 1, 0), 0, false);
            var chanceToGrowVertically = Math.Abs(branchEndPos.Y - logPos.Y) / (float)distance;
            var growVertically = random.NextFloat() < chanceToGrowVertically;
            logPos = logPos.Offset(growVertically ? verticalDirection : branchDirection);
            PlaceLog(level, trunkSetter, random, logPos, config,
                growVertically ? null : sidewaysStateModifier);
        }
    }
}
