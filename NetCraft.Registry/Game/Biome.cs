using NetCraft.Codec;
using NetCraft.Registry.Codec;
using NetCraft.Registry.Environment;

namespace NetCraft.Registry;

//Biome biome data class, maps to vanilla net.minecraft.world.level.biome.Biome
//All 5 DirectCodec members are inlined; the climate/attributes/effects/generation/generation settings fields are directly top-level biome JSON fields
public class Biome : RegistryIdentified
{
    //Plains plains placeholder instance used by Bootstrap and tests before data-driven loading is wired up; values match plains.json
    public static readonly Biome Plains = new Biome(
        new ClimateSettings(true, 0.8f, TemperatureModifier.None, 0.4f),
        EnvironmentAttributeMap.Empty,
        new BiomeSpecialEffects(unchecked((int)0xFF3F76E4), Optional<int>.Empty(), Optional<int>.Empty(), Optional<int>.Empty(),
            GrassColorModifier.None),
        BiomeGenerationSettings.Empty,
        MobSpawnSettings.Empty).WithId(Identifier.WithDefaultNamespace("plains"));

    //DirectCodec element codec, maps to vanilla Biome.DIRECT_CODEC
    public static readonly Codec<Biome> DirectCodec =
        RecordCodecBuilder.Of5<Biome, ClimateSettings, EnvironmentAttributeMap, BiomeSpecialEffects, BiomeGenerationSettings, MobSpawnSettings>(
            ClimateSettings.Codec.ForGetter<Biome, ClimateSettings>(b => b.Climate),
            EnvironmentAttributeMap.CodecOnlyPositional.OptionalFieldOf("attributes", EnvironmentAttributeMap.Empty)
                .ForGetter<Biome, EnvironmentAttributeMap>(b => b.Attributes),
            BiomeSpecialEffects.Codec.FieldOf("effects").ForGetter<Biome, BiomeSpecialEffects>(b => b.Effects),
            BiomeGenerationSettings.Codec.ForGetter<Biome, BiomeGenerationSettings>(b => b.Generation),
            MobSpawnSettings.Codec.ForGetter<Biome, MobSpawnSettings>(b => b.MobSettings),
            (climate, attributes, effects, generation, mobSettings) =>
                new Biome(climate, attributes, effects, generation, mobSettings));

    //NetworkCodec network sync codec, maps to vanilla Biome.NETWORK_CODEC and contains only climate/attributes/effects
    //Generation and generation settings are not synced and are emptied after decoding
    public static readonly Codec<Biome> NetworkCodec =
        RecordCodecBuilder.Of3<Biome, ClimateSettings, EnvironmentAttributeMap, BiomeSpecialEffects>(
            ClimateSettings.Codec.ForGetter<Biome, ClimateSettings>(b => b.Climate),
            EnvironmentAttributeMap.NetworkCodec.OptionalFieldOf("attributes", EnvironmentAttributeMap.Empty)
                .ForGetter<Biome, EnvironmentAttributeMap>(b => b.Attributes),
            BiomeSpecialEffects.Codec.FieldOf("effects").ForGetter<Biome, BiomeSpecialEffects>(b => b.Effects),
            (climate, attributes, effects) =>
                new Biome(climate, attributes, effects, BiomeGenerationSettings.Empty, MobSpawnSettings.Empty));

    //Codec looks up the BIOME registry by Identifier, the registry-reference form of vanilla Biome.CODEC
    //Used to resolve biome references like biome_is; not an element codec
    //BIOME is a DefaultedRegistry, so an unknown id returns the plains default
    public static readonly Codec<Biome> Codec = IdentifierCodec.Instance.ComapFlatMap(
        id => DataResult<Biome>.Success(BuiltInRegistries.BIOME.GetValue(id)!),
        biome => biome.Id);

    private Identifier? _id;

    public Biome(ClimateSettings climate, EnvironmentAttributeMap attributes, BiomeSpecialEffects effects,
        BiomeGenerationSettings generation, MobSpawnSettings mobSettings)
    {
        Climate = climate;
        Attributes = attributes;
        Effects = effects;
        Generation = generation;
        MobSettings = mobSettings;
    }

    //Protected parameterless constructor for placeholder subclasses; a placeholder implementing only Id gets empty defaults for climate/effects
    protected Biome()
        : this(
            new ClimateSettings(false, 0.5f, TemperatureModifier.None, 0.0f),
            EnvironmentAttributeMap.Empty,
            new BiomeSpecialEffects(unchecked((int)0xFF1E98E2), Optional<int>.Empty(), Optional<int>.Empty(), Optional<int>.Empty(),
                GrassColorModifier.None),
            BiomeGenerationSettings.Empty,
            MobSpawnSettings.Empty)
    {
    }

    public ClimateSettings Climate { get; }

    public EnvironmentAttributeMap Attributes { get; }

    public BiomeSpecialEffects Effects { get; }

    public BiomeGenerationSettings Generation { get; }

    public MobSpawnSettings MobSettings { get; }

    //Id the biome registry name; backfilled by the registry from the element path during data-driven loading, falling back to the registry default plains
    public virtual Identifier Id => _id ?? Identifier.WithDefaultNamespace("plains");

    //WithId backfills the registry name, used by the loading flow and placeholder instances
    public Biome WithId(Identifier id)
    {
        _id = id;
        return this;
    }

    //SetRegistryId is backfilled by the data-driven loading flow when writing into the registry
    public void SetRegistryId(Identifier id) => _id = id;
}
