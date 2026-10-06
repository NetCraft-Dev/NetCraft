using NetCraft.Codec;
using NetCraft.Registry.Codec;

namespace NetCraft.Registry;

//BiomeGenerationSettings biome generation settings, maps to vanilla BiomeGenerationSettings
//carvers maps to ConfiguredWorldCarver.LIST_CODEC and features maps to PlacedFeature.LIST_OF_LISTS_CODEC
//The outer features array is grouped by GenerationStep.Decoration index and the inner array holds the placed features referenced by this biome under that step
public sealed class BiomeGenerationSettings
{
    public static readonly BiomeGenerationSettings Empty = new(
        new DirectHolderSet<ConfiguredWorldCarver>(Array.Empty<Holder<ConfiguredWorldCarver>>()),
        Array.Empty<HolderSet<PlacedFeature>>());

    //FeaturesCodec codec for the outer/inner arrays; an empty inner array preserves the placeholder step
    public static readonly Codec<IReadOnlyList<HolderSet<PlacedFeature>>> FeaturesCodec =
        HolderSetCodecs.PlacedFeatureSet.ListOf();

    public static readonly Codec<BiomeGenerationSettings> Codec =
        RecordCodecBuilder.Of2<BiomeGenerationSettings, HolderSet<ConfiguredWorldCarver>, IReadOnlyList<HolderSet<PlacedFeature>>>(
            HolderSetCodecs.ConfiguredCarverSet.FieldOf("carvers")
                .ForGetter<BiomeGenerationSettings, HolderSet<ConfiguredWorldCarver>>(s => s.Carvers),
            FeaturesCodec.FieldOf("features").ForGetter<BiomeGenerationSettings, IReadOnlyList<HolderSet<PlacedFeature>>>(s => s.Features),
            (carvers, features) => new BiomeGenerationSettings(carvers, features));

    //Carvers carver set, bound to the configured carver registry
    public HolderSet<ConfiguredWorldCarver> Carvers { get; }

    //Features placed feature sets per step; the index is the GenerationStep.Decoration ordinal
    public IReadOnlyList<HolderSet<PlacedFeature>> Features { get; }

    public BiomeGenerationSettings(HolderSet<ConfiguredWorldCarver> carvers, IReadOnlyList<HolderSet<PlacedFeature>> features)
    {
        Carvers = carvers;
        Features = features;
    }
}
