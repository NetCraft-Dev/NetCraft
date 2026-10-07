using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Features;

//ConfiguredFeature configured feature, non-generic counterpart of vanilla ConfiguredFeature<FC,F>
//Binds a feature singleton to its config as a registry element; the type field dispatches and config goes through the feature's own codec
public sealed class ConfiguredFeature : NetCraft.Registry.ConfiguredFeature
{
    //Codec element codec, maps to vanilla DIRECT_CODEC
    public static readonly Codec<ConfiguredFeature> Codec = new ConfiguredFeatureCodec();

    //ElementCodec registry element codec; the registry holds elements by marker interface
    public static readonly Codec<NetCraft.Registry.ConfiguredFeature> ElementCodec = Codec.ComapFlatMap(
        feature => DataResult<NetCraft.Registry.ConfiguredFeature>.Success(feature),
        feature => (ConfiguredFeature)feature);

    public Feature Feature { get; }
    public FeatureConfiguration Config { get; }

    public ConfiguredFeature(Feature feature, FeatureConfiguration config)
    {
        Feature = feature;
        Config = config;
    }

    //Place place this feature, maps to vanilla place
    public bool Place(WorldGenRegion level, ChunkGenerator generator, RandomSource random, BlockPos origin)
        => Feature.Place(Config, new FeaturePlaceContext(level, generator, random, origin, Config));

    //SubFeatures embedded sub-feature references of this config
    public IEnumerable<Holder<NetCraft.Registry.ConfiguredFeature>> SubFeatures => Config.SubFeatures;

    public override string ToString() => $"{Feature.Id}[{Config.GetType().Name}]";
}

//ConfiguredFeatureCodec look up the FEATURE registry by the type field then decode config from the config field
//Maps to vanilla BuiltInRegistries.FEATURE.byNameCodec().dispatch(f => f, f => f.configuredCodec())
internal sealed class ConfiguredFeatureCodec : ScalarCodec<ConfiguredFeature>
{
    public override DataResult<ConfiguredFeature> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => DecodeFeature(ops, map));

    private static DataResult<ConfiguredFeature> DecodeFeature<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var typeTag = input.Get("type");
        if (!typeTag.IsPresent) return DataResult<ConfiguredFeature>.Error(() => "configured_feature is missing the type field");
        var typeText = ops.GetStringValue(typeTag.Get());
        if (!typeText.Result().IsPresent)
            return DataResult<ConfiguredFeature>.Error(() => "configured_feature type must be a string");
        var typeId = Identifier.TryParse(typeText.GetOrThrow());
        if (typeId is null)
            return DataResult<ConfiguredFeature>.Error(() => $"invalid feature type: {typeText.GetOrThrow()}");
        if (BuiltInRegistries.FEATURE.GetValue(typeId.Value) is not Feature feature)
            return DataResult<ConfiguredFeature>.Error(() => $"unknown feature type: {typeId}");

        var configTag = input.Get("config");
        if (!configTag.IsPresent)
            return DataResult<ConfiguredFeature>.Error(() => $"feature {typeId} is missing the config field");
        return feature.ConfigCodec.Parse(ops, configTag.Get()).Map(config => new ConfiguredFeature(feature, config));
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, ConfiguredFeature value)
    {
        var configResult = value.Feature.ConfigCodec.EncodeStart(ops, value.Config);
        if (!configResult.Result().IsPresent) return configResult;
        var builder = ops.MapBuilder();
        builder.Add("type", ops.CreateString(value.Feature.Id.ToString()));
        builder.Add("config", configResult.GetOrThrow());
        return builder.Build(ops.Empty());
    }
}
