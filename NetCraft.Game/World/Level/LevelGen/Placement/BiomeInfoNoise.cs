using NetCraft.Game.World.Level.LevelGen.Synth;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//BiomeInfoNoise 群系信息噪声对应原版 Biome.BIOME_INFO_NOISE
//噪声类计数修饰器靠它按坐标取值决定数量
internal static class BiomeInfoNoise
{
    //原版用 WorldgenRandom(new LegacyRandomSource(2345)) 包一层 委托随机源行为与直接构造一致
    private static readonly PerlinSimplexNoise Noise = new(new LegacyRandomSource(2345), new[] { 0 });

    public static double GetValue(double x, double z) => Noise.GetValue(x, z, false);
}
