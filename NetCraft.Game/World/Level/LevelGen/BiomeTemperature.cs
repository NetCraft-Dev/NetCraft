using NetCraft.Game.World.Level.LevelGen.Synth;
using NetCraft.Registry;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen;

//BiomeTemperature biome temperature check, maps to vanilla Biome.getHeightAdjustedTemperature and related predicates
//The temperature noise depends on the Synth layer's PerlinSimplexNoise; the Registry layer cannot reference it back, so this lives in its own class
public static class BiomeTemperature
{
    //Seeds and octaves of the three static noises copy vanilla Biome exactly, otherwise temperatures differ from the client
    private static readonly PerlinSimplexNoise TemperatureNoise =
        new(new LegacyRandomSource(1234L), new[] { 0 });
    private static readonly PerlinSimplexNoise FrozenTemperatureNoise =
        new(new LegacyRandomSource(3456L), new[] { -2, -1, 0 });
    private static readonly PerlinSimplexNoise BiomeInfoNoise =
        new(new LegacyRandomSource(2345L), new[] { 0 });

    //GetHeightAdjustedTemperature cools linearly with height above the snow line, maps to vanilla getHeightAdjustedTemperature
    public static float GetHeightAdjustedTemperature(Biome biome, int x, int y, int z, int seaLevel)
    {
        var adjusted = ModifyTemperature(biome, x, z);
        var snowLevel = seaLevel + 17;
        if (y > snowLevel)
        {
            var noise = (float)(TemperatureNoise.GetValue(x / 8.0f, z / 8.0f, false) * 8.0);
            return adjusted - ((noise + y - snowLevel) * 0.05f) / 40.0f;
        }
        return adjusted;
    }

    //ModifyTemperature applies the biome temperature_modifier; frozen biomes use the ice-patch noise to push the temperature down to 0.2
    private static float ModifyTemperature(Biome biome, int x, int z)
    {
        var climate = biome.Climate;
        if (climate.TemperatureModifier != TemperatureModifier.Frozen) return climate.Temperature;
        var largeVariation = FrozenTemperatureNoise.GetValue(x * 0.05, z * 0.05, false) * 7.0;
        var edgeVariation = BiomeInfoNoise.GetValue(x * 0.2, z * 0.2, false);
        if (largeVariation + edgeVariation < 0.3
            && BiomeInfoNoise.GetValue(x * 0.09, z * 0.09, false) < 0.8)
            return 0.2f;
        return climate.Temperature;
    }

    //ColdEnoughToSnow cold enough to snow, maps to vanilla coldEnoughToSnow
    public static bool ColdEnoughToSnow(Biome biome, int x, int y, int z, int seaLevel)
        => !WarmEnoughToRain(biome, x, y, z, seaLevel);

    //WarmEnoughToRain warm enough to rain, maps to vanilla warmEnoughToRain
    public static bool WarmEnoughToRain(Biome biome, int x, int y, int z, int seaLevel)
        => GetHeightAdjustedTemperature(biome, x, y, z, seaLevel) >= 0.15f;

    //ShouldMeltFrozenOceanIcebergSlightly iceberg melts slightly, maps to vanilla shouldMeltFrozenOceanIcebergSlightly
    public static bool ShouldMeltFrozenOceanIcebergSlightly(Biome biome, int x, int y, int z, int seaLevel)
        => GetHeightAdjustedTemperature(biome, x, y, z, seaLevel) > 0.1f;
}
