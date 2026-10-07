using NetCraft.Codec;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen.Features.Impl.Trees;

//FeatureSizeType feature size type base, maps to vanilla FeatureSizeType<P>
public abstract class FeatureSizeType : NetCraft.Registry.FeatureSizeType<object>
{
    public Identifier Id { get; }

    protected FeatureSizeType(Identifier id) => Id = id;

    public abstract DataResult<FeatureSize> Decode<U>(DynamicOps<U> ops, MapLike<U> input);

    public abstract void EncodeFields<U>(DynamicOps<U> ops, FeatureSize value, RecordBuilder<U> builder);
}

//FeatureSizeType<P> generic middle layer for a concrete size type
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

//FeatureSizeTypes built-in size type registration, maps to the static fields of vanilla FeatureSizeType
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

//FeatureSize feature size base, maps to vanilla FeatureSize
//The trunk pass-through range is given per height layer; larger sizes are more easily clipped by blocks above
public abstract class FeatureSize
{
    public static readonly Codec<FeatureSize> Codec = new FeatureSizeDispatchCodec();

    //Minimum allowed clipping height, absent when not given, maps to vanilla minClippedHeight
    public int? MinClippedHeight { get; }

    protected FeatureSize(int? minClippedHeight) => MinClippedHeight = minClippedHeight;

    public abstract FeatureSizeType Type { get; }

    public abstract int GetSizeAtHeight(int treeHeight, int yo);

    //ToOptionalInt convert a nullable int to Optional for writing back the min_clipped_height field
    protected static Optional<int> ToOptionalInt(int? value)
        => value is { } present ? Optional<int>.Of(present) : Optional<int>.Empty();
}

//FeatureSizeDispatchCodec look up FEATURE_SIZE_TYPE by the type field then delegate to that type
internal sealed class FeatureSizeDispatchCodec : ScalarCodec<FeatureSize>
{
    public override DataResult<FeatureSize> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => DecodeSize(ops, map));

    private static DataResult<FeatureSize> DecodeSize<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var typeTag = input.Get("type");
        if (!typeTag.IsPresent) return DataResult<FeatureSize>.Error(() => "feature size is missing the type field");
        var typeText = ops.GetStringValue(typeTag.Get());
        if (!typeText.Result().IsPresent)
            return DataResult<FeatureSize>.Error(() => "feature size type must be a string");
        var typeId = Identifier.TryParse(typeText.GetOrThrow());
        if (typeId is null)
            return DataResult<FeatureSize>.Error(() => $"invalid size type: {typeText.GetOrThrow()}");
        if (BuiltInRegistries.FEATURE_SIZE_TYPE.GetValue(typeId.Value) is not FeatureSizeType type)
            return DataResult<FeatureSize>.Error(() => $"unknown feature size type: {typeId}");
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

//TwoLayersFeatureSize two-layer size, maps to vanilla TwoLayersFeatureSize
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

//ThreeLayersFeatureSize three-layer size, maps to vanilla ThreeLayersFeatureSize
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
