using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//PosRuleTest rule position test, maps to vanilla PosRuleTest
//Decides whether a rule applies from the relative relation among the in-template position, world position and reference position
public abstract class PosRuleTest
{
    //Codec polymorphic entry, dispatches by predicate_type to a concrete type in the POS_RULE_TEST registry
    public static readonly Codec<PosRuleTest> Codec = new PosRuleTestDispatchCodec();

    //Test evaluates the position relation, maps to vanilla test
    public abstract bool Test(BlockPos inTemplatePos, BlockPos worldPos, BlockPos worldReference, RandomSource random);

    //Type the owning type singleton
    public abstract PosRuleTestType Type { get; }
}

//PosRuleTestType position test type, maps to vanilla PosRuleTestType
public abstract class PosRuleTestType : NetCraft.Registry.PosRuleTestType<object>
{
    public Identifier Id { get; }

    protected PosRuleTestType(Identifier id) => Id = id;

    //DecodeTest decodes a position test from a map
    public abstract DataResult<PosRuleTest> DecodeTest<U>(DynamicOps<U> ops, MapLike<U> input);

    public override string ToString() => $"PosRuleTestType[{Id}]";
}

//PosRuleTestType<T> strongly typed position test type
public sealed class PosRuleTestType<T> : PosRuleTestType where T : PosRuleTest
{
    private readonly MapCodec<T> _codec;

    public PosRuleTestType(Identifier id, MapCodec<T> codec) : base(id) => _codec = codec;

    public override DataResult<PosRuleTest> DecodeTest<U>(DynamicOps<U> ops, MapLike<U> input)
        => _codec.Decode(ops, input).Map(v => (PosRuleTest)v);
}

//PosRuleTestTypes position test type registration, maps to the static fields of vanilla PosRuleTestType
public static class PosRuleTestTypes
{
    public static readonly PosRuleTestType<PosAlwaysTrueTest> AlwaysTrue =
        Register("always_true", PosAlwaysTrueTest.MapCodec);

    public static readonly PosRuleTestType<LinearPosTest> LinearPos =
        Register("linear_pos", LinearPosTest.MapCodec);

    public static readonly PosRuleTestType<AxisAlignedLinearPosTest> AxisAlignedLinearPos =
        Register("axis_aligned_linear_pos", AxisAlignedLinearPosTest.MapCodec);

    //Register registers into POS_RULE_TEST and returns the type instance
    private static PosRuleTestType<T> Register<T>(string path, MapCodec<T> codec) where T : PosRuleTest
    {
        var type = new PosRuleTestType<T>(Identifier.WithDefaultNamespace(path), codec);
        Registry<NetCraft.Registry.PosRuleTestType<object>>.Register(BuiltInRegistries.POS_RULE_TEST, path, type);
        return type;
    }
}

//PosAlwaysTrueTest always-true position test, maps to vanilla PosAlwaysTrueTest
public sealed class PosAlwaysTrueTest : PosRuleTest
{
    public static readonly PosAlwaysTrueTest Instance = new();

    public static readonly MapCodec<PosAlwaysTrueTest> MapCodec =
        new StructureUnitMapCodec<PosAlwaysTrueTest>(() => Instance);

    private PosAlwaysTrueTest() { }

    public override bool Test(BlockPos inTemplatePos, BlockPos worldPos, BlockPos worldReference, RandomSource random) => true;

    public override PosRuleTestType Type => PosRuleTestTypes.AlwaysTrue;
}

//LinearPosTest position test with probability linearly interpolated by Manhattan distance, maps to vanilla LinearPosTest
public sealed class LinearPosTest : PosRuleTest
{
    public static readonly MapCodec<LinearPosTest> MapCodec =
        RecordCodecBuilder.Of4<LinearPosTest, float, float, int, int>(
            Codecs.Float.OptionalFieldOf("min_chance", 0.0f).ForGetter<LinearPosTest, float>(p => p.MinChance),
            Codecs.Float.OptionalFieldOf("max_chance", 0.0f).ForGetter<LinearPosTest, float>(p => p.MaxChance),
            Codecs.Int.OptionalFieldOf("min_dist", 0).ForGetter<LinearPosTest, int>(p => p.MinDist),
            Codecs.Int.OptionalFieldOf("max_dist", 0).ForGetter<LinearPosTest, int>(p => p.MaxDist),
            (minChance, maxChance, minDist, maxDist) => new LinearPosTest(minChance, maxChance, minDist, maxDist));

    public float MinChance { get; }
    public float MaxChance { get; }
    public int MinDist { get; }
    public int MaxDist { get; }

    public LinearPosTest(float minChance, float maxChance, int minDist, int maxDist)
    {
        if (minDist >= maxDist) throw new ArgumentException($"invalid distance range: [{minDist},{maxDist}]");
        MinChance = minChance;
        MaxChance = maxChance;
        MinDist = minDist;
        MaxDist = maxDist;
    }

    public override bool Test(BlockPos inTemplatePos, BlockPos worldPos, BlockPos worldReference, RandomSource random)
    {
        var dist = DistManhattan(worldPos, worldReference);
        var rnd = random.NextFloat();
        return rnd <= Mth.ClampedLerp(Mth.InverseLerp(dist, MinDist, MaxDist), MinChance, MaxChance);
    }

    //DistManhattan Manhattan distance between two points, maps to vanilla BlockPos.distManhattan
    internal static int DistManhattan(BlockPos a, BlockPos b)
        => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z);

    public override PosRuleTestType Type => PosRuleTestTypes.LinearPos;
}

//AxisAlignedLinearPosTest position test with probability linearly interpolated by single-axis distance, maps to vanilla AxisAlignedLinearPosTest
public sealed class AxisAlignedLinearPosTest : PosRuleTest
{
    public static readonly MapCodec<AxisAlignedLinearPosTest> MapCodec =
        RecordCodecBuilder.Of5<AxisAlignedLinearPosTest, float, float, int, int, Direction.Axis>(
            Codecs.Float.OptionalFieldOf("min_chance", 0.0f)
                .ForGetter<AxisAlignedLinearPosTest, float>(p => p.MinChance),
            Codecs.Float.OptionalFieldOf("max_chance", 0.0f)
                .ForGetter<AxisAlignedLinearPosTest, float>(p => p.MaxChance),
            Codecs.Int.OptionalFieldOf("min_dist", 0).ForGetter<AxisAlignedLinearPosTest, int>(p => p.MinDist),
            Codecs.Int.OptionalFieldOf("max_dist", 0).ForGetter<AxisAlignedLinearPosTest, int>(p => p.MaxDist),
            StructureAxisCodec.Instance.OptionalFieldOf("axis", Direction.Axis.Y)
                .ForGetter<AxisAlignedLinearPosTest, Direction.Axis>(p => p.Axis),
            (minChance, maxChance, minDist, maxDist, axis) =>
                new AxisAlignedLinearPosTest(minChance, maxChance, minDist, maxDist, axis));

    public float MinChance { get; }
    public float MaxChance { get; }
    public int MinDist { get; }
    public int MaxDist { get; }
    public Direction.Axis Axis { get; }

    public AxisAlignedLinearPosTest(float minChance, float maxChance, int minDist, int maxDist, Direction.Axis axis)
    {
        if (minDist >= maxDist) throw new ArgumentException($"invalid distance range: [{minDist},{maxDist}]");
        MinChance = minChance;
        MaxChance = maxChance;
        MinDist = minDist;
        MaxDist = maxDist;
        Axis = axis;
    }

    public override bool Test(BlockPos inTemplatePos, BlockPos worldPos, BlockPos worldReference, RandomSource random)
    {
        var direction = Direction.ByAxisDirection(Axis, Direction.AxisDirection.Positive);
        var xd = Math.Abs((worldPos.X - worldReference.X) * direction.StepX);
        var yd = Math.Abs((worldPos.Y - worldReference.Y) * direction.StepY);
        var zd = Math.Abs((worldPos.Z - worldReference.Z) * direction.StepZ);
        var dist = (int)(xd + yd + zd);
        var rnd = random.NextFloat();
        return rnd <= Mth.ClampedLerp(Mth.InverseLerp(dist, MinDist, MaxDist), MinChance, MaxChance);
    }

    public override PosRuleTestType Type => PosRuleTestTypes.AxisAlignedLinearPos;
}

//PosRuleTestDispatchCodec position test polymorphic codec, maps to the dispatch of vanilla PosRuleTest.CODEC
internal sealed class PosRuleTestDispatchCodec : ScalarCodec<PosRuleTest>
{
    public override DataResult<PosRuleTest> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => DecodeTest(ops, map));

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, PosRuleTest value)
        => DataResult<U>.Error(() => "position test encoding not implemented yet");

    //DecodeTest reads predicate_type, looks it up, then hands off to that type's codec
    internal static DataResult<PosRuleTest> DecodeTest<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var typeTag = input.Get("predicate_type");
        if (!typeTag.IsPresent) return DataResult<PosRuleTest>.Error(() => "position test is missing predicate_type");
        var text = ops.GetStringValue(typeTag.Get());
        if (!text.Result().IsPresent) return DataResult<PosRuleTest>.Error(() => "predicate_type must be a string");
        var id = Identifier.TryParse(text.GetOrThrow());
        if (id is null) return DataResult<PosRuleTest>.Error(() => $"invalid position test type: {text.GetOrThrow()}");
        if (!BuiltInRegistries.POS_RULE_TEST.ContainsKey(id.Value))
            return DataResult<PosRuleTest>.Error(() => $"unregistered position test type: {id}");
        var type = BuiltInRegistries.POS_RULE_TEST.GetValue(id.Value) as PosRuleTestType;
        return type is null
            ? DataResult<PosRuleTest>.Error(() => $"position test type {id} cannot be parsed")
            : type.DecodeTest(ops, input);
    }
}
