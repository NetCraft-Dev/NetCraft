using NetCraft.Codec;
using NetCraft.Logging;
using NetCraft.Registry;
using NetCraft.Util;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen;

//HeightProvider 高度提供者体系对应原版 net.minecraft.world.level.levelgen.heightproviders
//HeightProvider.Codec 锚点对象或按 type 字段派发二选一
public abstract class HeightProvider
{
    //Codec 锚点或带 type 对象二选一对应原版 CODEC
    public static readonly Codec<HeightProvider> Codec = new HeightProviderCodec();

    //Sample 按随机源与上下文采样高度对应原版 sample
    public abstract int Sample(RandomSource random, WorldGenerationContext context);
}

//ConstantHeight 常量高度提供者对应原版 ConstantHeight
public class ConstantHeight : HeightProvider
{
    //Zero 零高度单例对应原版 ZERO
    public static readonly ConstantHeight Zero = new(VerticalAnchor.Absolute(0));

    public VerticalAnchor Value { get; }

    private ConstantHeight(VerticalAnchor value) => Value = value;

    public static ConstantHeight Of(VerticalAnchor value) => new(value);

    public override int Sample(RandomSource random, WorldGenerationContext context) => Value.ResolveY(context);

    public override string ToString() => Value.ToString();
}

//UniformHeight 均匀分布高度提供者对应原版 UniformHeight
public class UniformHeight : HeightProvider
{
    private readonly HashSet<long> _warnedFor = new();

    public VerticalAnchor MinInclusive { get; }

    public VerticalAnchor MaxInclusive { get; }

    private UniformHeight(VerticalAnchor minInclusive, VerticalAnchor maxInclusive)
    {
        MinInclusive = minInclusive;
        MaxInclusive = maxInclusive;
    }

    public static UniformHeight Of(VerticalAnchor minInclusive, VerticalAnchor maxInclusive)
        => new(minInclusive, maxInclusive);

    //Sample 空区间返回下界并对每对上下界只警告一次对应原版 sample
    public override int Sample(RandomSource random, WorldGenerationContext context)
    {
        var min = MinInclusive.ResolveY(context);
        var max = MaxInclusive.ResolveY(context);
        if (min > max)
        {
            if (_warnedFor.Add(((long)min << 32) | (uint)max))
                Log.Warning($"Empty height range: {this}");
            return min;
        }
        return Mth.RandomBetweenInclusive(random, min, max);
    }

    public override string ToString() => $"[{MinInclusive}-{MaxInclusive}]";
}

//TrapezoidHeight 梯形分布高度提供者对应原版 TrapezoidHeight
//区间中段有一段平台 采样落在平台附近概率更高
public class TrapezoidHeight : HeightProvider
{
    public VerticalAnchor MinInclusive { get; }
    public VerticalAnchor MaxInclusive { get; }
    public int Plateau { get; }

    private TrapezoidHeight(VerticalAnchor minInclusive, VerticalAnchor maxInclusive, int plateau)
    {
        MinInclusive = minInclusive;
        MaxInclusive = maxInclusive;
        Plateau = plateau;
    }

    public static TrapezoidHeight Of(VerticalAnchor minInclusive, VerticalAnchor maxInclusive, int plateau)
        => new(minInclusive, maxInclusive, plateau);

    public static TrapezoidHeight Of(VerticalAnchor minInclusive, VerticalAnchor maxInclusive)
        => new(minInclusive, maxInclusive, 0);

    //Sample 平台宽度达到整个区间时退化成均匀分布对应原版 sample
    public override int Sample(RandomSource random, WorldGenerationContext context)
    {
        var min = MinInclusive.ResolveY(context);
        var max = MaxInclusive.ResolveY(context);
        if (min > max)
        {
            Log.Warning($"Empty height range: {this}");
            return min;
        }
        var range = max - min;
        if (Plateau >= range) return Mth.RandomBetweenInclusive(random, min, max);
        var plateauStart = (range - Plateau) / 2;
        var plateauEnd = range - plateauStart;
        return min + Mth.RandomBetweenInclusive(random, 0, plateauEnd)
            + Mth.RandomBetweenInclusive(random, 0, plateauStart);
    }

    public override string ToString() => Plateau == 0
        ? $"triangle ({MinInclusive}-{MaxInclusive})"
        : $"trapezoid({Plateau}) in [{MinInclusive}-{MaxInclusive}]";
}

//VeryBiasedToBottomHeight 强偏底高度提供者对应原版 VeryBiasedToBottomHeight
//连续三次取上界并逐次收窄 结果强烈偏向区间底部
public class VeryBiasedToBottomHeight : HeightProvider
{
    public VerticalAnchor MinInclusive { get; }
    public VerticalAnchor MaxInclusive { get; }
    public int Inner { get; }

    private VeryBiasedToBottomHeight(VerticalAnchor minInclusive, VerticalAnchor maxInclusive, int inner)
    {
        MinInclusive = minInclusive;
        MaxInclusive = maxInclusive;
        Inner = inner;
    }

    public static VeryBiasedToBottomHeight Of(VerticalAnchor minInclusive, VerticalAnchor maxInclusive, int inner)
        => new(minInclusive, maxInclusive, inner);

    //Sample 区间容不下内缩量时直接退回下界对应原版 sample
    public override int Sample(RandomSource random, WorldGenerationContext context)
    {
        var min = MinInclusive.ResolveY(context);
        var max = MaxInclusive.ResolveY(context);
        if (max - min - Inner + 1 <= 0)
        {
            Log.Warning($"Empty height range: {this}");
            return min;
        }
        var upperInclusive = Mth.RandomBetweenInclusive(random, min + Inner, max);
        var biasedUpperInclusive = Mth.RandomBetweenInclusive(random, min, upperInclusive - 1);
        return Mth.RandomBetweenInclusive(random, min, biasedUpperInclusive - 1 + Inner);
    }

    public override string ToString() => $"biased[{MinInclusive}-{MaxInclusive} inner: {Inner}]";
}

//HeightProviderCodec 高度提供者编解码
//VerticalAnchor 形式对象解为 ConstantHeight 其余按 type 字段派发
internal sealed class HeightProviderCodec : ScalarCodec<HeightProvider>
{
    public override DataResult<HeightProvider> Parse<U>(DynamicOps<U> ops, U input)
    {
        var anchor = VerticalAnchor.Codec.Parse(ops, input);
        if (anchor.Result().IsPresent)
            return DataResult<HeightProvider>.Success(ConstantHeight.Of(anchor.GetOrThrow()));
        return ops.GetMap(input).FlatMap(map => DecodeProvider(ops, map));
    }

    private static DataResult<HeightProvider> DecodeProvider<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var type = ReadType(ops, input);
        if (!type.IsPresent)
            return DataResult<HeightProvider>.Error(() => "missing type field for HeightProvider");
        switch (PathOf(type.Get()))
        {
            case "constant":
                return ReadAnchor(ops, input, "value").Map(anchor => (HeightProvider)ConstantHeight.Of(anchor));
            case "uniform":
            {
                var bounds = ReadBounds(ops, input);
                if (!bounds.Result().IsPresent)
                    return DataResult<HeightProvider>.Error(() => Failure("bounds", bounds));
                var (min, max) = bounds.GetOrThrow();
                return DataResult<HeightProvider>.Success(UniformHeight.Of(min, max));
            }
            case "trapezoid":
            {
                var bounds = ReadBounds(ops, input);
                if (!bounds.Result().IsPresent)
                    return DataResult<HeightProvider>.Error(() => Failure("bounds", bounds));
                var (min, max) = bounds.GetOrThrow();
                return DataResult<HeightProvider>.Success(
                    TrapezoidHeight.Of(min, max, ReadOptionalInt(ops, input, "plateau", 0)));
            }
            case "very_biased_to_bottom":
            {
                var bounds = ReadBounds(ops, input);
                if (!bounds.Result().IsPresent)
                    return DataResult<HeightProvider>.Error(() => Failure("bounds", bounds));
                var (min, max) = bounds.GetOrThrow();
                return DataResult<HeightProvider>.Success(
                    VeryBiasedToBottomHeight.Of(min, max, ReadOptionalInt(ops, input, "inner", 1)));
            }
            default:
                return DataResult<HeightProvider>.Error(() => $"unknown height provider type: {type.Get()}");
        }
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, HeightProvider value)
    {
        //常量编码成裸锚点对象保持与原版 either 的编码方向一致
        if (value is ConstantHeight constant)
            return VerticalAnchor.Codec.EncodeStart(ops, constant.Value);
        var builder = ops.MapBuilder();
        switch (value)
        {
            case UniformHeight uniform:
                builder.Add("type", ops.CreateString(Identifier.WithDefaultNamespace("uniform").ToString()));
                builder.Add("min_inclusive", AnchorValue(ops, uniform.MinInclusive));
                builder.Add("max_inclusive", AnchorValue(ops, uniform.MaxInclusive));
                break;
            case TrapezoidHeight trapezoid:
                builder.Add("type", ops.CreateString(Identifier.WithDefaultNamespace("trapezoid").ToString()));
                builder.Add("min_inclusive", AnchorValue(ops, trapezoid.MinInclusive));
                builder.Add("max_inclusive", AnchorValue(ops, trapezoid.MaxInclusive));
                builder.Add("plateau", ops.CreateInt(trapezoid.Plateau));
                break;
            case VeryBiasedToBottomHeight biased:
                builder.Add("type",
                    ops.CreateString(Identifier.WithDefaultNamespace("very_biased_to_bottom").ToString()));
                builder.Add("min_inclusive", AnchorValue(ops, biased.MinInclusive));
                builder.Add("max_inclusive", AnchorValue(ops, biased.MaxInclusive));
                builder.Add("inner", ops.CreateInt(biased.Inner));
                break;
            default:
                return DataResult<U>.Error(() => $"unsupported HeightProvider: {value?.GetType().Name}");
        }
        return builder.Build(ops.Empty());
    }

    //AnchorValue 编码单个锚点失败直接抛出
    private static U AnchorValue<U>(DynamicOps<U> ops, VerticalAnchor anchor)
        => VerticalAnchor.Codec.EncodeStart(ops, anchor).GetOrThrow();

    //ReadAnchor 读锚点字段缺失或非法都返回错误
    private static DataResult<VerticalAnchor> ReadAnchor<U>(DynamicOps<U> ops, MapLike<U> input, string name)
    {
        var tag = input.Get(name);
        return tag.IsPresent
            ? VerticalAnchor.Codec.Parse(ops, tag.Get())
            : DataResult<VerticalAnchor>.Error(() => $"missing field: {name}");
    }

    //ReadType 读 type 字符串字段缺失或非字符串返回空
    private static Optional<string> ReadType<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var tag = input.Get("type");
        if (!tag.IsPresent) return Optional<string>.Empty();
        var text = ops.GetStringValue(tag.Get());
        return text.Result().IsPresent ? Optional<string>.Of(text.GetOrThrow()) : Optional<string>.Empty();
    }

    //PathOf 带命名空间的 type 只取路径段
    private static string PathOf(string type)
    {
        var id = Identifier.TryParse(type);
        return id is not null ? id.Value.Path : type;
    }

    //ReadBounds 一次读出 min_inclusive 与 max_inclusive 两个锚点字段
    private static DataResult<(VerticalAnchor Min, VerticalAnchor Max)> ReadBounds<U>(
        DynamicOps<U> ops, MapLike<U> input)
    {
        var min = ReadAnchor(ops, input, "min_inclusive");
        if (!min.Result().IsPresent)
            return DataResult<(VerticalAnchor, VerticalAnchor)>.Error(() => Failure("min_inclusive", min));
        var max = ReadAnchor(ops, input, "max_inclusive");
        return max.Result().IsPresent
            ? DataResult<(VerticalAnchor, VerticalAnchor)>.Success((min.GetOrThrow(), max.GetOrThrow()))
            : DataResult<(VerticalAnchor, VerticalAnchor)>.Error(() => Failure("max_inclusive", max));
    }

    //ReadOptionalInt 读可选整数标量字段 缺失或非数字时用默认值
    private static int ReadOptionalInt<U>(DynamicOps<U> ops, MapLike<U> input, string name, int defaultValue)
    {
        var tag = input.Get(name);
        if (!tag.IsPresent) return defaultValue;
        var value = ops.GetNumberValue(tag.Get());
        return value.Result().IsPresent ? (int)value.GetOrThrow() : defaultValue;
    }

    //Failure 组装字段解码失败的报错带上内层原因
    private static string Failure<T>(string name, DataResult<T> result)
    {
        var cause = string.Empty;
        result.ResultOrPartial(msg => cause = msg);
        return $"failed to decode {name}: {cause}";
    }
}
