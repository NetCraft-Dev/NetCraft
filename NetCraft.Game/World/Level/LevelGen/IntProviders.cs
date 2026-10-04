using NetCraft.Codec;
using NetCraft.Registry;
using NetCraft.Util;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen;

//IntProvider 整数提供者体系对应原版 net.minecraft.util.valueproviders
//IntProviders.Codec 裸整数或按 type 字段派发二选一 类型集合是封闭的 与 FloatProviders 一样手写派发
public abstract class IntProvider : NetCraft.Registry.IntProvider
{
    //Sample 按随机源采样一个整数对应原版 sample
    public abstract int Sample(RandomSource random);

    //MinInclusive 取值下界对应原版 minInclusive
    public abstract int MinInclusive { get; }

    //MaxInclusive 取值上界对应原版 maxInclusive
    public abstract int MaxInclusive { get; }
}

//ConstantInt 常量整数提供者对应原版 ConstantInt
public sealed class ConstantInt : IntProvider
{
    //Zero 常量零单例对应原版 ZERO
    public static readonly ConstantInt Zero = new(0);

    public int Value { get; }

    public ConstantInt(int value) => Value = value;

    //Of 零值复用单例对应原版 of
    public static ConstantInt Of(int value) => value == 0 ? Zero : new ConstantInt(value);

    public override int Sample(RandomSource random) => Value;

    public override int MinInclusive => Value;

    public override int MaxInclusive => Value;

    public override string ToString() => Value.ToString();
}

//UniformInt 均匀分布整数提供者对应原版 UniformInt
public sealed class UniformInt : IntProvider
{
    public UniformInt(int minInclusive, int maxInclusive)
    {
        MinInclusive = minInclusive;
        MaxInclusive = maxInclusive;
    }

    public static UniformInt Of(int minInclusive, int maxInclusive) => new(minInclusive, maxInclusive);

    public override int MinInclusive { get; }

    public override int MaxInclusive { get; }

    public override int Sample(RandomSource random) => Mth.RandomBetweenInclusive(random, MinInclusive, MaxInclusive);

    public override string ToString() => $"[{MinInclusive}-{MaxInclusive}]";
}

//BiasedToBottomInt 偏向低值分布的整数提供者对应原版 BiasedToBottomInt
public sealed class BiasedToBottomInt : IntProvider
{
    public BiasedToBottomInt(int minInclusive, int maxInclusive)
    {
        MinInclusive = minInclusive;
        MaxInclusive = maxInclusive;
    }

    public static BiasedToBottomInt Of(int minInclusive, int maxInclusive) => new(minInclusive, maxInclusive);

    public override int MinInclusive { get; }

    public override int MaxInclusive { get; }

    //Sample 两次取随机构成偏向低值的分布对应原版 sample 调用顺序不能变
    public override int Sample(RandomSource random)
        => MinInclusive + random.NextInt(random.NextInt(MaxInclusive - MinInclusive + 1) + 1);

    public override string ToString() => $"[{MinInclusive}-{MaxInclusive}]";
}

//ClampedInt 截断包装的整数提供者对应原版 ClampedInt
public sealed class ClampedInt : IntProvider
{
    public IntProvider Source { get; }

    public ClampedInt(IntProvider source, int minInclusive, int maxInclusive)
    {
        Source = source;
        MinInclusive = minInclusive;
        MaxInclusive = maxInclusive;
    }

    public static ClampedInt Of(IntProvider source, int minInclusive, int maxInclusive)
        => new(source, minInclusive, maxInclusive);

    public override int MinInclusive { get; }

    public override int MaxInclusive { get; }

    public override int Sample(RandomSource random)
        => Mth.Clamp(Source.Sample(random), MinInclusive, MaxInclusive);
}

//ClampedNormalInt 正态分布再截断的整数提供者对应原版 ClampedNormalInt
public sealed class ClampedNormalInt : IntProvider
{
    public float Mean { get; }
    public float Deviation { get; }

    public ClampedNormalInt(float mean, float deviation, int minInclusive, int maxInclusive)
    {
        Mean = mean;
        Deviation = deviation;
        MinInclusive = minInclusive;
        MaxInclusive = maxInclusive;
    }

    public static ClampedNormalInt Of(float mean, float deviation, int minInclusive, int maxInclusive)
        => new(mean, deviation, minInclusive, maxInclusive);

    public override int MinInclusive { get; }

    public override int MaxInclusive { get; }

    //Sample 先按正态采样再截断最后取整 顺序与原版一致
    public override int Sample(RandomSource random)
        => (int)Mth.Clamp(Mth.Normal(random, Mean, Deviation), MinInclusive, MaxInclusive);

    public override string ToString() => $"normal({Mean}, {Deviation}) in [{MinInclusive}-{MaxInclusive}]";
}

//TrapezoidInt 梯形分布整数提供者对应原版 TrapezoidInt
public sealed class TrapezoidInt : IntProvider
{
    public int Plateau { get; }

    public TrapezoidInt(int minInclusive, int maxInclusive, int plateau)
    {
        MinInclusive = minInclusive;
        MaxInclusive = maxInclusive;
        Plateau = plateau;
    }

    public static TrapezoidInt Of(int min, int max, int plateau) => new(min, max, plateau);

    public static IntProvider Triangle(int range) => Of(-range, range, 0);

    public override int MinInclusive { get; }

    public override int MaxInclusive { get; }

    public override int Sample(RandomSource random)
    {
        //对称无平台时退化成两个独立随机数相减 对应原版快路径
        if (Plateau == 0 && MaxInclusive == -MinInclusive)
            return random.NextInt(MaxInclusive + 1) - random.NextInt(MaxInclusive + 1);
        var range = MaxInclusive - MinInclusive;
        if (Plateau == range) return Mth.RandomBetweenInclusive(random, MinInclusive, MaxInclusive);
        var plateauStart = (range - Plateau) / 2;
        var plateauEnd = range - plateauStart;
        return MinInclusive + Mth.RandomBetweenInclusive(random, 0, plateauEnd)
            + Mth.RandomBetweenInclusive(random, 0, plateauStart);
    }

    public override string ToString() => $"trapezoid({Plateau}) in [{MinInclusive}-{MaxInclusive}]";
}

//WeightedListInt 权重列表整数提供者对应原版 WeightedListInt
public sealed class WeightedListInt : IntProvider
{
    public WeightedList<IntProvider> Distribution { get; }

    public WeightedListInt(WeightedList<IntProvider> distribution)
    {
        Distribution = distribution;
        var min = int.MaxValue;
        var max = int.MinValue;
        foreach (var entry in distribution.Unwrap())
        {
            min = Math.Min(min, entry.Value.MinInclusive);
            max = Math.Max(max, entry.Value.MaxInclusive);
        }
        MinInclusive = min;
        MaxInclusive = max;
    }

    public override int MinInclusive { get; }

    public override int MaxInclusive { get; }

    public override int Sample(RandomSource random) => Distribution.GetRandomOrThrow(random).Sample(random);
}

//IntProviders 整数提供者 codec 入口对应原版 IntProviders
public static class IntProviders
{
    //Codec 裸整数或带 type 对象二选一对应原版 CODEC
    public static readonly Codec<IntProvider> Codec = new IntProviderCodec();

    //NonNegativeCodec 要求整个取值范围非负对应原版 NON_NEGATIVE_CODEC
    public static readonly Codec<IntProvider> NonNegativeCodec = new RangeValidatedIntProviderCodec(Codec, 0, int.MaxValue);

    //PositiveCodec 要求整个取值范围为正对应原版 POSITIVE_CODEC
    public static readonly Codec<IntProvider> PositiveCodec = new RangeValidatedIntProviderCodec(Codec, 1, int.MaxValue);
}

//RangeValidatedIntProviderCodec 给整数提供者加取值区间校验
//count 这类字段不允许出现负值 提前报错比生成时算出负数好排查
internal sealed class RangeValidatedIntProviderCodec : ScalarCodec<IntProvider>
{
    private readonly Codec<IntProvider> _source;
    private readonly int _min;
    private readonly int _max;

    public RangeValidatedIntProviderCodec(Codec<IntProvider> source, int min, int max)
    {
        _source = source;
        _min = min;
        _max = max;
    }

    public override DataResult<IntProvider> Parse<U>(DynamicOps<U> ops, U input)
        => _source.Parse(ops, input).FlatMap(value =>
        {
            if (value.MinInclusive < _min)
                return DataResult<IntProvider>.Error(() =>
                    $"取值下界过小 应为 {_min} 实际 [{value.MinInclusive}-{value.MaxInclusive}]");
            if (value.MaxInclusive > _max)
                return DataResult<IntProvider>.Error(() =>
                    $"取值上界过大 应为 {_max} 实际 [{value.MinInclusive}-{value.MaxInclusive}]");
            return DataResult<IntProvider>.Success(value);
        });

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, IntProvider value)
        => _source.EncodeStart(ops, value);
}

//IntProviderCodec 整数提供者编解码 裸数字解为 ConstantInt 对象按 type 字段派发
internal sealed class IntProviderCodec : ScalarCodec<IntProvider>
{
    public override DataResult<IntProvider> Parse<U>(DynamicOps<U> ops, U input)
    {
        var number = ops.GetNumberValue(input);
        if (number.Result().IsPresent)
            return DataResult<IntProvider>.Success(ConstantInt.Of((int)number.GetOrThrow()));
        return ops.GetMap(input).FlatMap(map => DecodeProvider(ops, map));
    }

    private static DataResult<IntProvider> DecodeProvider<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var type = ReadString(ops, input, "type");
        if (type is null) return DataResult<IntProvider>.Error(() => "整数提供者缺 type 字段");
        switch (Identifier.TryParse(type)?.Path ?? type)
        {
            case "constant":
            {
                var value = ReadInt(ops, input, "value");
                return value is null
                    ? DataResult<IntProvider>.Error(() => "constant 整数提供者缺 value 字段")
                    : DataResult<IntProvider>.Success(ConstantInt.Of(value.Value));
            }
            case "uniform":
            {
                var min = ReadInt(ops, input, "min_inclusive");
                var max = ReadInt(ops, input, "max_inclusive");
                if (min is null || max is null)
                    return DataResult<IntProvider>.Error(() => "uniform 需要 min_inclusive 与 max_inclusive");
                if (max < min)
                    return DataResult<IntProvider>.Error(() => $"上界必须不小于下界 [{min}-{max}]");
                return DataResult<IntProvider>.Success(new UniformInt(min.Value, max.Value));
            }
            case "biased_to_bottom":
            {
                var min = ReadInt(ops, input, "min_inclusive");
                var max = ReadInt(ops, input, "max_inclusive");
                if (min is null || max is null)
                    return DataResult<IntProvider>.Error(() => "biased_to_bottom 需要 min_inclusive 与 max_inclusive");
                if (max < min)
                    return DataResult<IntProvider>.Error(() => $"上界必须不小于下界 [{min}-{max}]");
                return DataResult<IntProvider>.Success(new BiasedToBottomInt(min.Value, max.Value));
            }
            case "clamped":
            {
                var sourceTag = input.Get("source");
                if (!sourceTag.IsPresent)
                    return DataResult<IntProvider>.Error(() => "clamped 缺 source 字段");
                var source = IntProviders.Codec.Parse(ops, sourceTag.Get());
                if (!source.Result().IsPresent)
                    return DataResult<IntProvider>.Error(() => "clamped 的 source 解析失败");
                var min = ReadInt(ops, input, "min_inclusive");
                var max = ReadInt(ops, input, "max_inclusive");
                if (min is null || max is null)
                    return DataResult<IntProvider>.Error(() => "clamped 需要 min_inclusive 与 max_inclusive");
                if (max < min)
                    return DataResult<IntProvider>.Error(() => $"上界必须不小于下界 [{min}-{max}]");
                return DataResult<IntProvider>.Success(new ClampedInt(source.GetOrThrow(), min.Value, max.Value));
            }
            case "clamped_normal":
            {
                var mean = ReadFloat(ops, input, "mean");
                var deviation = ReadFloat(ops, input, "deviation");
                var min = ReadInt(ops, input, "min_inclusive");
                var max = ReadInt(ops, input, "max_inclusive");
                if (mean is null || deviation is null || min is null || max is null)
                    return DataResult<IntProvider>.Error(() => "clamped_normal 需要 mean/deviation/min_inclusive/max_inclusive");
                if (max < min)
                    return DataResult<IntProvider>.Error(() => $"上界必须不小于下界 [{min}-{max}]");
                return DataResult<IntProvider>.Success(
                    new ClampedNormalInt(mean.Value, deviation.Value, min.Value, max.Value));
            }
            case "trapezoid":
            {
                var min = ReadInt(ops, input, "min");
                var max = ReadInt(ops, input, "max");
                var plateau = ReadInt(ops, input, "plateau");
                if (min is null || max is null || plateau is null)
                    return DataResult<IntProvider>.Error(() => "trapezoid 需要 min/max/plateau");
                if (max < min)
                    return DataResult<IntProvider>.Error(() => $"上界必须不小于下界 [{min}-{max}]");
                if (plateau > max - min)
                    return DataResult<IntProvider>.Error(() => $"平台宽度不能超过全跨度 [{min}-{max}]");
                return DataResult<IntProvider>.Success(new TrapezoidInt(min.Value, max.Value, plateau.Value));
            }
            case "weighted_list":
            {
                var distributionTag = input.Get("distribution");
                if (!distributionTag.IsPresent)
                    return DataResult<IntProvider>.Error(() => "weighted_list 缺 distribution 字段");
                var stream = ops.GetStream(distributionTag.Get());
                if (!stream.Result().IsPresent)
                    return DataResult<IntProvider>.Error(() => "weighted_list 的 distribution 必须是数组");
                var entries = new List<Weighted<IntProvider>>();
                foreach (var element in stream.GetOrThrow())
                {
                    var elementMap = ops.GetMap(element);
                    if (!elementMap.Result().IsPresent)
                        return DataResult<IntProvider>.Error(() => "weighted_list 的元素必须是对象");
                    var map = elementMap.GetOrThrow();
                    var dataTag = map.Get("data");
                    if (!dataTag.IsPresent)
                        return DataResult<IntProvider>.Error(() => "weighted_list 的元素缺 data 字段");
                    var data = IntProviders.Codec.Parse(ops, dataTag.Get());
                    if (!data.Result().IsPresent)
                        return DataResult<IntProvider>.Error(() => "weighted_list 的 data 解析失败");
                    var weight = ReadInt(ops, map, "weight") ?? 1;
                    entries.Add(new Weighted<IntProvider>(data.GetOrThrow(), weight));
                }
                if (entries.Count == 0)
                    return DataResult<IntProvider>.Error(() => "weighted_list 至少需要一个元素");
                return DataResult<IntProvider>.Success(new WeightedListInt(WeightedList<IntProvider>.Of(entries)));
            }
            default:
                return DataResult<IntProvider>.Error(() => $"未知的整数提供者类型: {type}");
        }
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, IntProvider value)
    {
        //常量编码成裸整数保持与原版 either 的编码方向一致
        if (value is ConstantInt constant)
            return DataResult<U>.Success(ops.CreateInt(constant.Value));
        var builder = ops.MapBuilder();
        switch (value)
        {
            case UniformInt uniform:
                builder.Add("type", ops.CreateString("minecraft:uniform"));
                builder.Add("min_inclusive", ops.CreateInt(uniform.MinInclusive));
                builder.Add("max_inclusive", ops.CreateInt(uniform.MaxInclusive));
                break;
            case BiasedToBottomInt biased:
                builder.Add("type", ops.CreateString("minecraft:biased_to_bottom"));
                builder.Add("min_inclusive", ops.CreateInt(biased.MinInclusive));
                builder.Add("max_inclusive", ops.CreateInt(biased.MaxInclusive));
                break;
            case ClampedNormalInt normal:
                builder.Add("type", ops.CreateString("minecraft:clamped_normal"));
                builder.Add("mean", ops.CreateFloat(normal.Mean));
                builder.Add("deviation", ops.CreateFloat(normal.Deviation));
                builder.Add("min_inclusive", ops.CreateInt(normal.MinInclusive));
                builder.Add("max_inclusive", ops.CreateInt(normal.MaxInclusive));
                break;
            case TrapezoidInt trapezoid:
                builder.Add("type", ops.CreateString("minecraft:trapezoid"));
                builder.Add("min", ops.CreateInt(trapezoid.MinInclusive));
                builder.Add("max", ops.CreateInt(trapezoid.MaxInclusive));
                builder.Add("plateau", ops.CreateInt(trapezoid.Plateau));
                break;
            case ClampedInt clamped:
            {
                var source = EncodeStart(ops, clamped.Source);
                if (!source.Result().IsPresent) return source;
                builder.Add("type", ops.CreateString("minecraft:clamped"));
                builder.Add("source", source.GetOrThrow());
                builder.Add("min_inclusive", ops.CreateInt(clamped.MinInclusive));
                builder.Add("max_inclusive", ops.CreateInt(clamped.MaxInclusive));
                break;
            }
            default:
                return DataResult<U>.Error(() => $"暂不支持编码该整数提供者: {value.GetType().Name}");
        }
        return builder.Build(ops.Empty());
    }

    //ReadString 读字符串字段缺失或非字符串返回 null
    private static string? ReadString<U>(DynamicOps<U> ops, MapLike<U> input, string name)
    {
        var tag = input.Get(name);
        if (!tag.IsPresent) return null;
        var text = ops.GetStringValue(tag.Get());
        return text.Result().IsPresent ? text.GetOrThrow() : null;
    }

    //ReadInt 读整数字段缺失或非数字返回 null
    private static int? ReadInt<U>(DynamicOps<U> ops, MapLike<U> input, string name)
    {
        var tag = input.Get(name);
        if (!tag.IsPresent) return null;
        var value = ops.GetNumberValue(tag.Get());
        return value.Result().IsPresent ? (int)value.GetOrThrow() : null;
    }

    //ReadFloat 读浮点字段缺失或非数字返回 null
    private static float? ReadFloat<U>(DynamicOps<U> ops, MapLike<U> input, string name)
    {
        var tag = input.Get(name);
        if (!tag.IsPresent) return null;
        var value = ops.GetNumberValue(tag.Get());
        return value.Result().IsPresent ? (float)value.GetOrThrow() : null;
    }
}
