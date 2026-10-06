using NetCraft.Codec;

namespace NetCraft.Registry;

//Precipitation precipitation form, maps to vanilla Biome.Precipitation
public enum Precipitation
{
    None,
    Rain,
    Snow
}

//TemperatureModifier temperature modifier, maps to vanilla Biome.TemperatureModifier
//Only the real data for frozen_ocean/deep_frozen_ocean uses frozen
public enum TemperatureModifier
{
    None,
    Frozen
}

//ClimateSettings biome climate settings, maps to vanilla Biome.ClimateSettings
//The four fields are inlined into Biome.DirectCodec and are directly top-level fields of the biome JSON
public sealed class ClimateSettings
{
    //TemperatureModifierCodec temperature modifier enum codec
    public static readonly Codec<TemperatureModifier> TemperatureModifierCodec = new StringEnumCodec<TemperatureModifier>(
        (TemperatureModifier.None, "none"),
        (TemperatureModifier.Frozen, "frozen"));

    public static readonly Codec<ClimateSettings> Codec = RecordCodecBuilder.Of4<ClimateSettings, bool, float, TemperatureModifier, float>(
        Codecs.Bool.FieldOf("has_precipitation").ForGetter<ClimateSettings, bool>(c => c.HasPrecipitation),
        Codecs.Float.FieldOf("temperature").ForGetter<ClimateSettings, float>(c => c.Temperature),
        TemperatureModifierCodec.OptionalFieldOf("temperature_modifier", TemperatureModifier.None)
            .ForGetter<ClimateSettings, TemperatureModifier>(c => c.TemperatureModifier),
        Codecs.Float.FieldOf("downfall").ForGetter<ClimateSettings, float>(c => c.Downfall),
        (hasPrecipitation, temperature, temperatureModifier, downfall) =>
            new ClimateSettings(hasPrecipitation, temperature, temperatureModifier, downfall));

    public bool HasPrecipitation { get; }

    public float Temperature { get; }

    public TemperatureModifier TemperatureModifier { get; }

    public float Downfall { get; }

    public ClimateSettings(bool hasPrecipitation, float temperature, TemperatureModifier temperatureModifier, float downfall)
    {
        HasPrecipitation = hasPrecipitation;
        Temperature = temperature;
        TemperatureModifier = temperatureModifier;
        Downfall = downfall;
    }

    //GetSerializedName precipitation form serialized name, maps to vanilla StringRepresentable
    public static string GetSerializedName(Precipitation precipitation) => precipitation switch
    {
        Precipitation.Rain => "rain",
        Precipitation.Snow => "snow",
        _ => "none"
    };
}
