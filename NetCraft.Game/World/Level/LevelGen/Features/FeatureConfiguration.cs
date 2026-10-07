using NetCraft.Codec;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen.Features;

//FeatureConfiguration feature configuration base, maps to vanilla FeatureConfiguration
//Subclasses hold their own parameters; embedded sub-feature references are exposed through SubFeatures for decoration counting and weight selection
public abstract class FeatureConfiguration
{
    //SubFeatures embedded sub-feature references of this config, maps to vanilla getSubFeatures
    public virtual IEnumerable<Holder<NetCraft.Registry.ConfiguredFeature>> SubFeatures
        => Array.Empty<Holder<NetCraft.Registry.ConfiguredFeature>>();
}

//NoneFeatureConfiguration parameterless config, maps to vanilla NoneFeatureConfiguration
//Written as an empty object in JSON; decoding yields the shared singleton
public sealed class NoneFeatureConfiguration : FeatureConfiguration
{
    public static readonly NoneFeatureConfiguration Instance = new();

    public static readonly Codec<NoneFeatureConfiguration> Codec = new UnitCodec();

    private NoneFeatureConfiguration() { }
}

//UnitCodec parameterless config codec ignoring input and returning the singleton, maps to vanilla MapCodec.unit
internal sealed class UnitCodec : ScalarCodec<NoneFeatureConfiguration>
{
    public override DataResult<NoneFeatureConfiguration> Parse<U>(DynamicOps<U> ops, U input)
        => DataResult<NoneFeatureConfiguration>.Success(NoneFeatureConfiguration.Instance);

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, NoneFeatureConfiguration value)
        => DataResult<U>.Success(ops.Empty());
}
