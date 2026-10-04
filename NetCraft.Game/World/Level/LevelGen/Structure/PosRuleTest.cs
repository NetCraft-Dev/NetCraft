using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//PosRuleTest 规则位置判定 对应原版 PosRuleTest
//按模板内坐标 世界坐标 参考坐标三者的相对关系决定这条规则是否生效
public abstract class PosRuleTest
{
    //Codec 多态入口 按 predicate_type 派发到 POS_RULE_TEST 注册表里的具体类型
    public static readonly Codec<PosRuleTest> Codec = new PosRuleTestDispatchCodec();

    //Test 判定位置关系 对应原版 test
    public abstract bool Test(BlockPos inTemplatePos, BlockPos worldPos, BlockPos worldReference, RandomSource random);

    //Type 所属类型单例
    public abstract PosRuleTestType Type { get; }
}

//PosRuleTestType 位置判定类型 对应原版 PosRuleTestType
public abstract class PosRuleTestType : NetCraft.Registry.PosRuleTestType<object>
{
    public Identifier Id { get; }

    protected PosRuleTestType(Identifier id) => Id = id;

    //DecodeTest 从 map 解出一条位置判定
    public abstract DataResult<PosRuleTest> DecodeTest<U>(DynamicOps<U> ops, MapLike<U> input);

    public override string ToString() => $"PosRuleTestType[{Id}]";
}

//PosRuleTestType<T> 强类型位置判定类型
public sealed class PosRuleTestType<T> : PosRuleTestType where T : PosRuleTest
{
    private readonly MapCodec<T> _codec;

    public PosRuleTestType(Identifier id, MapCodec<T> codec) : base(id) => _codec = codec;

    public override DataResult<PosRuleTest> DecodeTest<U>(DynamicOps<U> ops, MapLike<U> input)
        => _codec.Decode(ops, input).Map(v => (PosRuleTest)v);
}

//PosRuleTestTypes 位置判定类型登记 对应原版 PosRuleTestType 的静态字段
public static class PosRuleTestTypes
{
    public static readonly PosRuleTestType<PosAlwaysTrueTest> AlwaysTrue =
        Register("always_true", PosAlwaysTrueTest.MapCodec);

    public static readonly PosRuleTestType<LinearPosTest> LinearPos =
        Register("linear_pos", LinearPosTest.MapCodec);

    public static readonly PosRuleTestType<AxisAlignedLinearPosTest> AxisAlignedLinearPos =
        Register("axis_aligned_linear_pos", AxisAlignedLinearPosTest.MapCodec);

    //Register 登记进 POS_RULE_TEST 并返回类型实例
    private static PosRuleTestType<T> Register<T>(string path, MapCodec<T> codec) where T : PosRuleTest
    {
        var type = new PosRuleTestType<T>(Identifier.WithDefaultNamespace(path), codec);
        Registry<NetCraft.Registry.PosRuleTestType<object>>.Register(BuiltInRegistries.POS_RULE_TEST, path, type);
        return type;
    }
}

//PosAlwaysTrueTest 恒真位置判定 对应原版 PosAlwaysTrueTest
public sealed class PosAlwaysTrueTest : PosRuleTest
{
    public static readonly PosAlwaysTrueTest Instance = new();

    public static readonly MapCodec<PosAlwaysTrueTest> MapCodec =
        new StructureUnitMapCodec<PosAlwaysTrueTest>(() => Instance);

    private PosAlwaysTrueTest() { }

    public override bool Test(BlockPos inTemplatePos, BlockPos worldPos, BlockPos worldReference, RandomSource random) => true;

    public override PosRuleTestType Type => PosRuleTestTypes.AlwaysTrue;
}

//LinearPosTest 按曼哈顿距离线性插值概率的位置判定 对应原版 LinearPosTest
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
        if (minDist >= maxDist) throw new ArgumentException($"距离区间非法: [{minDist},{maxDist}]");
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

    //DistManhattan 两点的曼哈顿距离 对应原版 BlockPos.distManhattan
    internal static int DistManhattan(BlockPos a, BlockPos b)
        => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z);

    public override PosRuleTestType Type => PosRuleTestTypes.LinearPos;
}

//AxisAlignedLinearPosTest 沿单轴距离线性插值概率的位置判定 对应原版 AxisAlignedLinearPosTest
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
        if (minDist >= maxDist) throw new ArgumentException($"距离区间非法: [{minDist},{maxDist}]");
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

//PosRuleTestDispatchCodec 位置判定多态 codec 对应原版 PosRuleTest.CODEC 的 dispatch
internal sealed class PosRuleTestDispatchCodec : ScalarCodec<PosRuleTest>
{
    public override DataResult<PosRuleTest> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => DecodeTest(ops, map));

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, PosRuleTest value)
        => DataResult<U>.Error(() => "位置判定编码暂未实现");

    //DecodeTest 读 predicate_type 查表再交给该类型的 codec
    internal static DataResult<PosRuleTest> DecodeTest<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var typeTag = input.Get("predicate_type");
        if (!typeTag.IsPresent) return DataResult<PosRuleTest>.Error(() => "位置判定缺少 predicate_type");
        var text = ops.GetStringValue(typeTag.Get());
        if (!text.Result().IsPresent) return DataResult<PosRuleTest>.Error(() => "predicate_type 必须是字符串");
        var id = Identifier.TryParse(text.GetOrThrow());
        if (id is null) return DataResult<PosRuleTest>.Error(() => $"非法的位置判定类型: {text.GetOrThrow()}");
        if (!BuiltInRegistries.POS_RULE_TEST.ContainsKey(id.Value))
            return DataResult<PosRuleTest>.Error(() => $"未注册的位置判定类型: {id}");
        var type = BuiltInRegistries.POS_RULE_TEST.GetValue(id.Value) as PosRuleTestType;
        return type is null
            ? DataResult<PosRuleTest>.Error(() => $"位置判定类型 {id} 无法解析")
            : type.DecodeTest(ops, input);
    }
}
