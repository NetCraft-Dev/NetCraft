using NetCraft.Codec;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen.Features.Impl.Trees;

//FeatureSizeType 特征尺寸类型基类 对应原版 FeatureSizeType<P>
public abstract class FeatureSizeType : NetCraft.Registry.FeatureSizeType<object>
{
    public Identifier Id { get; }

    protected FeatureSizeType(Identifier id) => Id = id;

    public abstract DataResult<FeatureSize> Decode<U>(DynamicOps<U> ops, MapLike<U> input);

    public abstract void EncodeFields<U>(DynamicOps<U> ops, FeatureSize value, RecordBuilder<U> builder);
}

//FeatureSizeType<P> 具体尺寸类型的泛型中间层
public abstract class FeatureSizeType<P> : FeatureSizeType where P : FeatureSize
{
    private readonly MapCodec<P> _codec;

    protected FeatureSizeType(Identifier id, MapCodec<P> codec) : base(id) => _codec = codec;

    public sealed override DataResult<FeatureSize> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => _codec.Decode(ops, input).Map(value => (FeatureSize)value);

    public sealed override void EncodeFields<U>(DynamicOps<U> ops, FeatureSize value, RecordBuilder<U> builder)
    {
        if (value is P typed) _codec.EncodeTo(ops, typed, builder);
    }
}

internal sealed class SimpleFeatureSizeType<P> : FeatureSizeType<P> where P : FeatureSize
{
    public SimpleFeatureSizeType(string id, MapCodec<P> codec)
        : base(Identifier.WithDefaultNamespace(id), codec) { }
}

//FeatureSizeTypes 内置尺寸类型登记 对应原版 FeatureSizeType 的静态字段
public static class FeatureSizeTypes
{
    public static readonly FeatureSizeType<TwoLayersFeatureSize> TwoLayers =
        Register("two_layers_feature_size", TwoLayersFeatureSize.Codec);

    public static readonly FeatureSizeType<ThreeLayersFeatureSize> ThreeLayers =
        Register("three_layers_feature_size", ThreeLayersFeatureSize.Codec);

    private static FeatureSizeType<T> Register<T>(string path, MapCodec<T> codec) where T : FeatureSize
    {
        var type = new SimpleFeatureSizeType<T>(path, codec);
        Registry<NetCraft.Registry.FeatureSizeType<object>>.Register(
            BuiltInRegistries.FEATURE_SIZE_TYPE, type.Id, type);
        return type;
    }
}

//FeatureSize 特征尺寸基类 对应原版 FeatureSize
//树干可穿行范围按高度分层给定 尺寸越大越容易被上方方块截断
public abstract class FeatureSize
{
    public static readonly Codec<FeatureSize> Codec = new FeatureSizeDispatchCodec();

    //最小允许截断高度 未给时为空 对应原版 minClippedHeight
    public int? MinClippedHeight { get; }

    protected FeatureSize(int? minClippedHeight) => MinClippedHeight = minClippedHeight;

    public abstract FeatureSizeType Type { get; }

    public abstract int GetSizeAtHeight(int treeHeight, int yo);

    //ToOptionalInt 可空整数转 Optional 供 min_clipped_height 字段回写
    protected static Optional<int> ToOptionalInt(int? value)
        => value is { } present ? Optional<int>.Of(present) : Optional<int>.Empty();
}

//FeatureSizeDispatchCodec 按 type 字段查 FEATURE_SIZE_TYPE 再委派给该类型
internal sealed class FeatureSizeDispatchCodec : ScalarCodec<FeatureSize>
{
    public override DataResult<FeatureSize> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => DecodeSize(ops, map));

    private static DataResult<FeatureSize> DecodeSize<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var typeTag = input.Get("type");
        if (!typeTag.IsPresent) return DataResult<FeatureSize>.Error(() => "特征尺寸缺 type 字段");
        var typeText = ops.GetStringValue(typeTag.Get());
        if (!typeText.Result().IsPresent)
            return DataResult<FeatureSize>.Error(() => "特征尺寸的 type 必须是字符串");
        var typeId = Identifier.TryParse(typeText.GetOrThrow());
        if (typeId is null)
            return DataResult<FeatureSize>.Error(() => $"非法的尺寸类型: {typeText.GetOrThrow()}");
        if (BuiltInRegistries.FEATURE_SIZE_TYPE.GetValue(typeId.Value) is not FeatureSizeType type)
            return DataResult<FeatureSize>.Error(() => $"未知的特征尺寸类型: {typeId}");
        return type.Decode(ops, input);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, FeatureSize value)
    {
        var builder = ops.MapBuilder();
        builder.Add("type", ops.CreateString(value.Type.Id.ToString()));
        value.Type.EncodeFields(ops, value, builder);
        return builder.Build(ops.Empty());
    }
}

//TwoLayersFeatureSize 两层尺寸 对应原版 TwoLayersFeatureSize
public sealed class TwoLayersFeatureSize : FeatureSize
{
    public static readonly MapCodec<TwoLayersFeatureSize> Codec =
        RecordCodecBuilder.Of4<TwoLayersFeatureSize, int, int, int, Optional<int>>(
            Codecs.Int.OptionalFieldOf("limit", 1).ForGetter<TwoLayersFeatureSize, int>(s => s.Limit),
            Codecs.Int.OptionalFieldOf("lower_size", 0).ForGetter<TwoLayersFeatureSize, int>(s => s.LowerSize),
            Codecs.Int.OptionalFieldOf("upper_size", 1).ForGetter<TwoLayersFeatureSize, int>(s => s.UpperSize),
            Codecs.Int.OptionalFieldOf("min_clipped_height")
                .ForGetter<TwoLayersFeatureSize, Optional<int>>(s => ToOptionalInt(s.MinClippedHeight)),
            (limit, lowerSize, upperSize, minClippedHeight) => new TwoLayersFeatureSize(limit, lowerSize,
                upperSize, minClippedHeight.IsPresent ? minClippedHeight.Get() : null));

    public int Limit { get; }
    public int LowerSize { get; }
    public int UpperSize { get; }

    public TwoLayersFeatureSize(int limit, int lowerSize, int upperSize, int? minClippedHeight)
        : base(minClippedHeight)
    {
        Limit = limit;
        LowerSize = lowerSize;
        UpperSize = upperSize;
    }

    public override FeatureSizeType Type => FeatureSizeTypes.TwoLayers;

    public override int GetSizeAtHeight(int treeHeight, int yo) => yo < Limit ? LowerSize : UpperSize;
}

//ThreeLayersFeatureSize 三层尺寸 对应原版 ThreeLayersFeatureSize
public sealed class ThreeLayersFeatureSize : FeatureSize
{
    public static readonly MapCodec<ThreeLayersFeatureSize> Codec =
        RecordCodecBuilder.Of6<ThreeLayersFeatureSize, int, int, int, int, int, Optional<int>>(
            Codecs.Int.OptionalFieldOf("limit", 1).ForGetter<ThreeLayersFeatureSize, int>(s => s.Limit),
            Codecs.Int.OptionalFieldOf("upper_limit", 1)
                .ForGetter<ThreeLayersFeatureSize, int>(s => s.UpperLimit),
            Codecs.Int.OptionalFieldOf("lower_size", 0)
                .ForGetter<ThreeLayersFeatureSize, int>(s => s.LowerSize),
            Codecs.Int.OptionalFieldOf("middle_size", 1)
                .ForGetter<ThreeLayersFeatureSize, int>(s => s.MiddleSize),
            Codecs.Int.OptionalFieldOf("upper_size", 1)
                .ForGetter<ThreeLayersFeatureSize, int>(s => s.UpperSize),
            Codecs.Int.OptionalFieldOf("min_clipped_height")
                .ForGetter<ThreeLayersFeatureSize, Optional<int>>(
                    s => ToOptionalInt(s.MinClippedHeight)),
            (limit, upperLimit, lowerSize, middleSize, upperSize, minClippedHeight) =>
                new ThreeLayersFeatureSize(limit, upperLimit, lowerSize, middleSize, upperSize,
                    minClippedHeight.IsPresent ? minClippedHeight.Get() : null));

    public int Limit { get; }
    public int UpperLimit { get; }
    public int LowerSize { get; }
    public int MiddleSize { get; }
    public int UpperSize { get; }

    public ThreeLayersFeatureSize(int limit, int upperLimit, int lowerSize, int middleSize, int upperSize,
        int? minClippedHeight)
        : base(minClippedHeight)
    {
        Limit = limit;
        UpperLimit = upperLimit;
        LowerSize = lowerSize;
        MiddleSize = middleSize;
        UpperSize = upperSize;
    }

    public override FeatureSizeType Type => FeatureSizeTypes.ThreeLayers;

    public override int GetSizeAtHeight(int treeHeight, int yo)
    {
        if (yo < Limit) return LowerSize;
        if (yo >= treeHeight - UpperLimit) return UpperSize;
        return MiddleSize;
    }
}
