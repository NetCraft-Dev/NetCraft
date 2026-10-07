using NetCraft.Game.World.Level.LevelGen.Synth;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//BiomeInfoNoise biome info noise, maps to vanilla Biome.BIOME_INFO_NOISE
//Noise-based count modifiers sample it by coordinate to decide the count
internal static class BiomeInfoNoise
{
    //Vanilla wraps WorldgenRandom(new LegacyRandomSource(2345)); the delegating random source behaves the same as constructing it directly
    private static readonly PerlinSimplexNoise Noise = new(new LegacyRandomSource(2345), new[] { 0 });

    public static double GetValue(double x, double z) => Noise.GetValue(x, z, false);
}
