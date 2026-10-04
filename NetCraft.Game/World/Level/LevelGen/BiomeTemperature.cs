using NetCraft.Game.World.Level.LevelGen.Synth;
using NetCraft.Registry;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen;

//BiomeTemperature 群系温度判定对应原版 Biome 的 getHeightAdjustedTemperature 与相关谓词
//温度噪声依赖 Synth 层的 PerlinSimplexNoise Registry 层不能反向引用 因此这里单独成类
public static class BiomeTemperature
{
    //三个静态噪声的种子与八度照抄原版 Biome 温度曲线才能与客户端一致
    private static readonly PerlinSimplexNoise TemperatureNoise =
        new(new LegacyRandomSource(1234L), new[] { 0 });
    private static readonly PerlinSimplexNoise FrozenTemperatureNoise =
        new(new LegacyRandomSource(3456L), new[] { -2, -1, 0 });
    private static readonly PerlinSimplexNoise BiomeInfoNoise =
        new(new LegacyRandomSource(2345L), new[] { 0 });

    //GetHeightAdjustedTemperature 雪线以上按高度线性降温 对应原版 getHeightAdjustedTemperature
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

    //ModifyTemperature 应用群系的 temperature_modifier 冻原用冰斑块噪声把温度压到 0.2
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

    //ColdEnoughToSnow 冷到下雪 对应原版 coldEnoughToSnow
    public static bool ColdEnoughToSnow(Biome biome, int x, int y, int z, int seaLevel)
        => !WarmEnoughToRain(biome, x, y, z, seaLevel);

    //WarmEnoughToRain 暖到下雨 对应原版 warmEnoughToRain
    public static bool WarmEnoughToRain(Biome biome, int x, int y, int z, int seaLevel)
        => GetHeightAdjustedTemperature(biome, x, y, z, seaLevel) >= 0.15f;

    //ShouldMeltFrozenOceanIcebergSlightly 冰山轻微融化 对应原版 shouldMeltFrozenOceanIcebergSlightly
    public static bool ShouldMeltFrozenOceanIcebergSlightly(Biome biome, int x, int y, int z, int seaLevel)
        => GetHeightAdjustedTemperature(biome, x, y, z, seaLevel) > 0.1f;
}
