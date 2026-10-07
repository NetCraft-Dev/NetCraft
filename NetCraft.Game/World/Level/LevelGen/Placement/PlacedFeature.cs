using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Random;
using GameConfiguredFeature = NetCraft.Game.World.Level.LevelGen.Features.ConfiguredFeature;
using RegistryConfiguredFeature = NetCraft.Registry.ConfiguredFeature;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//PlacedFeature placed feature, maps to vanilla PlacedFeature
//Binds a configured feature to a chain of placement modifiers; decoration computes positions first then tries to place each
public sealed class PlacedFeature : NetCraft.Registry.PlacedFeature
{
    //Codec element codec, maps to vanilla DIRECT_CODEC
    public static readonly Codec<PlacedFeature> Codec = new PlacedFeatureCodec();

    //ElementCodec registry element codec; the registry holds elements by marker interface
    public static readonly Codec<NetCraft.Registry.PlacedFeature> ElementCodec = Codec.ComapFlatMap(
        feature => DataResult<NetCraft.Registry.PlacedFeature>.Success(feature),
        feature => (PlacedFeature)feature);

    //Feature the referenced configured feature
    public Holder<RegistryConfiguredFeature> Feature { get; }

    //Placement the placement modifier chain; applied in sequence to get the final candidate positions
    public IReadOnlyList<PlacementModifier> Placement { get; }

    public PlacedFeature(Holder<RegistryConfiguredFeature> feature, IReadOnlyList<PlacementModifier> placement)
    {
        Feature = feature;
        Placement = placement;
    }

    //Place place this feature, maps to vanilla place
    public bool Place(WorldGenRegion level, ChunkGenerator generator, RandomSource random, BlockPos origin)
        => PlaceWithContext(new PlacementContext(level, generator, null), random, origin);

    //PlaceWithBiomeCheck placement with a biome check, maps to vanilla placeWithBiomeCheck
    //The decoration pipeline enters here so the biome modifier can look up which biomes this feature belongs to
    public bool PlaceWithBiomeCheck(WorldGenRegion level, ChunkGenerator generator, RandomSource random, BlockPos origin)
        => PlaceWithContext(new PlacementContext(level, generator, this), random, origin);

    //PlaceWithContext run the modifier chain to get candidate positions, then try placing at each
    private bool PlaceWithContext(PlacementContext context, RandomSource random, BlockPos origin)
    {
        IEnumerable<BlockPos> positions = new[] { origin };
        foreach (var modifier in Placement)
        {
            var source = positions;
            var current = modifier;
            positions = source.SelectMany(pos => current.GetPositions(context, random, pos));
        }

        var placedAny = false;
        foreach (var pos in positions)
        {
            if (Feature.Value is GameConfiguredFeature configured
                && configured.Place(context.Level, context.Generator, random, pos))
                placedAny = true;
        }
        return placedAny;
    }

    //GetFeatures every configured feature referenced by this feature, including sub-features embedded in the config
    public IEnumerable<Holder<RegistryConfiguredFeature>> GetFeatures()
    {
        yield return Feature;
        if (Feature.Value is GameConfiguredFeature configured)
        {
            foreach (var sub in configured.SubFeatures) yield return sub;
        }
    }

    public override string ToString() => $"Placed {Feature.RegisteredName}";
}

//PlacedFeatureCodec decode {feature, placement}, maps to vanilla DIRECT_CODEC
//feature is a registered reference string and placement is the modifier array
internal sealed class PlacedFeatureCodec : ScalarCodec<PlacedFeature>
{
    public override DataResult<PlacedFeature> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => DecodePlacedFeature(ops, map));

    private static DataResult<PlacedFeature> DecodePlacedFeature<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var featureTag = input.Get("feature");
        if (!featureTag.IsPresent) return DataResult<PlacedFeature>.Error(() => "placed_feature is missing the feature field");
        var featureResult = ConfiguredFeatureInlineRefCodec.Instance.Parse(ops, featureTag.Get());
        if (!featureResult.Result().IsPresent)
            return DataResult<PlacedFeature>.Error(() => "placed_feature feature failed to parse: "
                + featureResult.MapOrElse(_ => string.Empty, error => error));

        var placementTag = input.Get("placement");
        if (!placementTag.IsPresent)
            return DataResult<PlacedFeature>.Error(() => "placed_feature is missing the placement field");
        var streamResult = ops.GetStream(placementTag.Get());
        if (!streamResult.Result().IsPresent)
            return DataResult<PlacedFeature>.Error(() => "placed_feature placement must be an array");

        var modifiers = new List<PlacementModifier>();
        foreach (var element in streamResult.GetOrThrow())
        {
            var modifierResult = PlacementModifierCodec.Instance.Parse(ops, element);
            if (!modifierResult.Result().IsPresent)
                return DataResult<PlacedFeature>.Error(() => $"placed_feature placement modifier #{modifiers.Count} failed to parse: "
                    + modifierResult.MapOrElse(_ => string.Empty, error => error));
            modifiers.Add(modifierResult.GetOrThrow());
        }

        return DataResult<PlacedFeature>.Success(new PlacedFeature(featureResult.GetOrThrow(), modifiers));
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, PlacedFeature value)
    {
        var featureResult = ConfiguredFeatureInlineRefCodec.Instance.EncodeStart(ops, value.Feature);
        if (!featureResult.Result().IsPresent) return featureResult;
        var modifiers = new List<U>();
        foreach (var modifier in value.Placement)
        {
            var modifierResult = PlacementModifierCodec.Instance.EncodeStart(ops, modifier);
            if (!modifierResult.Result().IsPresent) return modifierResult;
            modifiers.Add(modifierResult.GetOrThrow());
        }

        var builder = ops.MapBuilder();
        builder.Add("feature", featureResult.GetOrThrow());
        builder.Add("placement", ops.CreateList(modifiers));
        return builder.Build(ops.Empty());
    }
}

//ConfiguredFeatureInlineRefCodec configured feature reference codec accepting both registry names and inline definitions
//Maps to the allowInline form of vanilla ConfiguredFeature.CODEC
//The feature field of placed_feature can be either "minecraft:oak" or a full inline definition
internal sealed class ConfiguredFeatureInlineRefCodec : ScalarCodec<Holder<RegistryConfiguredFeature>>
{
    public static readonly ConfiguredFeatureInlineRefCodec Instance = new();

    public override DataResult<Holder<RegistryConfiguredFeature>> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (text.Result().IsPresent) return HolderSetCodecs.ConfiguredFeatureRef.Parse(ops, input);
        var inline = GameConfiguredFeature.Codec.Parse(ops, input);
        if (inline.Result().IsPresent)
            return DataResult<Holder<RegistryConfiguredFeature>>.Success(
                Holder<RegistryConfiguredFeature>.Direct(inline.GetOrThrow()));
        var reason = inline.MapOrElse(_ => string.Empty, error => error);
        return DataResult<Holder<RegistryConfiguredFeature>>.Error(
            () => $"configured feature is neither a registry name nor an inline definition: {reason}");
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Holder<RegistryConfiguredFeature> value)
        => value.UnwrapKey() is null && value.Value is GameConfiguredFeature inline
            ? GameConfiguredFeature.Codec.EncodeStart(ops, inline)
            : HolderSetCodecs.ConfiguredFeatureRef.EncodeStart(ops, value);
}
