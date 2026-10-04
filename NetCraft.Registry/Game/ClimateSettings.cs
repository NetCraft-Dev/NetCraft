using NetCraft.Codec;

namespace NetCraft.Registry;

//Precipitation 降水形态对应原版 Biome.Precipitation
public enum Precipitation
{
    None,
    Rain,
    Snow
}

//TemperatureModifier 温度修正对应原版 Biome.TemperatureModifier
//只有 frozen_ocean/deep_frozen_ocean 的真实数据用到 frozen
public enum TemperatureModifier
{
    None,
    Frozen
}

//ClimateSettings 群系气候设置对应原版 Biome.ClimateSettings
//四个字段内联进 Biome.DirectCodec 即直接是群系 JSON 的顶层字段
public sealed class ClimateSettings
{
    //TemperatureModifierCodec 温度修正枚举 codec
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

    //GetSerializedName 降水形态序列化名对应原版 StringRepresentable
    public static string GetSerializedName(Precipitation precipitation) => precipitation switch
    {
        Precipitation.Rain => "rain",
        Precipitation.Snow => "snow",
        _ => "none"
    };
}
