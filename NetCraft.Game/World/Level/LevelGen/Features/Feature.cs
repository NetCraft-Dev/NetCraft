using NetCraft.Codec;
using NetCraft.Game.World.Level.Block;
using NetCraft.Registry;
using NetCraft.Registry.State;

namespace NetCraft.Game.World.Level.LevelGen.Features;

//Feature abstract feature base, non-generic counterpart of vanilla Feature<FC>
//Vanilla parameterizes by config type; NetCraft has no covariant generics, so a generic middle layer Feature<FC> carries the concrete config
//The FEATURE registry and ConfiguredFeature dispatch depend only on this layer
public abstract class Feature : NetCraft.Registry.Feature
{
    //Air air state, used for the replaceability check before placing
    protected static readonly BlockState Air = Blocks.AIR.DefaultBlockState;

    //Id registry name, matching the key in the FEATURE registry
    public Identifier Id { get; }

    protected Feature(Identifier id) => Id = id;

    //ConfigCodec codec decoding this feature's config, used by ConfiguredFeature dispatch
    public abstract Codec<FeatureConfiguration> ConfigCodec { get; }

    //Place place the feature with its config, maps to vanilla place(FC, WorldGenLevel, ChunkGenerator, RandomSource, BlockPos)
    public abstract bool Place(FeatureConfiguration config, FeaturePlaceContext context);

    //Register register into the FEATURE registry and return itself, so static fields can assign it directly
    protected static T Register<T>(Identifier id, T feature) where T : Feature
    {
        Registry<NetCraft.Registry.Feature>.Register(BuiltInRegistries.FEATURE, id, feature);
        return feature;
    }
}

//Feature<FC> generic middle layer with a concrete config type; subclasses only implement Place(FC, context)
public abstract class Feature<FC> : Feature where FC : FeatureConfiguration
{
    private readonly Codec<FC> _codec;
    private UpcastCodec<FC>? _upcast;

    protected Feature(Identifier id, Codec<FC> codec) : base(id) => _codec = codec;

    protected abstract bool Place(FC config, FeaturePlaceContext context);

    public sealed override bool Place(FeatureConfiguration config, FeaturePlaceContext context)
        => Place((FC)config, context);

    public sealed override Codec<FeatureConfiguration> ConfigCodec => _upcast ??= new UpcastCodec<FC>(_codec);
}

//UpcastCodec adapt a concrete config codec to the base codec for the non-generic dispatch
//The decoded object already is an FC instance; this only upcasts the type
internal sealed class UpcastCodec<FC> : ScalarCodec<FeatureConfiguration> where FC : FeatureConfiguration
{
    private readonly Codec<FC> _inner;

    public UpcastCodec(Codec<FC> inner) => _inner = inner;

    public override DataResult<FeatureConfiguration> Parse<U>(DynamicOps<U> ops, U input)
        => _inner.Parse(ops, input).Map(value => (FeatureConfiguration)value);

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, FeatureConfiguration value)
        => value is FC typed
            ? _inner.EncodeStart(ops, typed)
            : DataResult<U>.Error(() => $"config type mismatch, expected {typeof(FC).Name} but got {value.GetType().Name}");
}
