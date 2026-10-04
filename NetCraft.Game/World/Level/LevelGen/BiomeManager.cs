using System.Security.Cryptography;
using NetCraft.Registry;
using NetCraft.Storage;

namespace NetCraft.Game.World.Level.LevelGen;

//BiomeManager 生物群系管理器 对应原版 net.minecraft.world.level.biome.BiomeManager
//按方块坐标找最近的 quart 采样点 优先查已生成区块的群系调色板 出界才回退完整气候采样
//地表阶段每一格都要问一次群系 原版正是靠这条调色板查询把代价压到常数级
//直接把 BiomeSource 当 getter 传进去会退化成每格一次六维气候采样 区块生成慢到超时
public sealed class BiomeManager
{
    //_chunk 当前正在生成的区块 群系调色板在 BIOMES 阶段已填好
    private readonly ChunkAccess _chunk;
    //_fallback 出界时的完整采样 入参用 quart 对应的方块坐标 与 BIOMES 阶段写入调色板的坐标一致
    private readonly Func<int, int, int, Biome> _fallback;
    private readonly long _biomeZoomSeed;
    private readonly int _minQuartY;
    private readonly int _maxQuartY;

    public BiomeManager(ChunkAccess chunk, Func<int, int, int, Biome> fallback, long seed)
    {
        _chunk = chunk;
        _fallback = fallback;
        _biomeZoomSeed = ObfuscateSeed(seed);
        _minQuartY = chunk.MinSectionY * 4;
        _maxQuartY = (chunk.MinSectionY + chunk.SectionsCount) * 4;
    }

    //ObfuscateSeed 打乱世界种子 对应原版 obfuscateSeed 的 sha256(seed).asLong()
    //原版 HashCode.asLong 按小端读前八字节 输入也按小端写入 这里照做否则模糊距离全不一样
    public static long ObfuscateSeed(long seed)
    {
        Span<byte> input = stackalloc byte[8];
        for (var i = 0; i < 8; i++) input[i] = (byte)(seed >> (i * 8));
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(input, hash);
        var result = 0L;
        for (var i = 0; i < 8; i++) result |= (long)hash[i] << (i * 8);
        return result;
    }

    //GetBiome 按方块坐标取最近采样点的群系 对应原版 getBiome
    //把坐标往负方向挪两格再按四格分块 八个角点各算一次模糊距离取最小
    //挪两格是让采样点落在方块中心而不是边界 模糊距离避免大片规则网格
    public Biome GetBiome(int blockX, int blockY, int blockZ)
    {
        var absoluteX = blockX - 2;
        var absoluteY = blockY - 2;
        var absoluteZ = blockZ - 2;
        var parentX = absoluteX >> 2;
        var parentY = absoluteY >> 2;
        var parentZ = absoluteZ >> 2;
        var fractX = (absoluteX & 3) / 4.0;
        var fractY = (absoluteY & 3) / 4.0;
        var fractZ = (absoluteZ & 3) / 4.0;
        var nearestCorner = 0;
        var nearestDistance = double.PositiveInfinity;
        for (var i = 0; i < 8; i++)
        {
            var xEven = (i & 4) == 0;
            var yEven = (i & 2) == 0;
            var zEven = (i & 1) == 0;
            var cornerX = xEven ? parentX : parentX + 1;
            var cornerY = yEven ? parentY : parentY + 1;
            var cornerZ = zEven ? parentZ : parentZ + 1;
            var distanceX = xEven ? fractX : fractX - 1.0;
            var distanceY = yEven ? fractY : fractY - 1.0;
            var distanceZ = zEven ? fractZ : fractZ - 1.0;
            var distance = FiddledDistance(_biomeZoomSeed, cornerX, cornerY, cornerZ,
                distanceX, distanceY, distanceZ);
            if (distance >= nearestDistance) continue;
            nearestCorner = i;
            nearestDistance = distance;
        }
        var quartX = (nearestCorner & 4) == 0 ? parentX : parentX + 1;
        var quartY = (nearestCorner & 2) == 0 ? parentY : parentY + 1;
        var quartZ = (nearestCorner & 1) == 0 ? parentZ : parentZ + 1;
        return GetNoiseBiome(quartX, quartY, quartZ);
    }

    //GetNoiseBiome 按 quart 坐标取群系 落在本区块内查调色板 出界回退完整采样 对应原版 LevelReader.getNoiseBiome
    private Biome GetNoiseBiome(int quartX, int quartY, int quartZ)
    {
        if (quartX >> 2 == _chunk.Pos.X && quartZ >> 2 == _chunk.Pos.Z
            && quartY >= _minQuartY && quartY < _maxQuartY)
            return _chunk.GetNoiseBiome(quartX, quartY, quartZ).Value;
        return _fallback(quartX * 4, quartY * 4, quartZ * 4);
    }

    //FiddledDistance 加了模糊偏移的平方距离 对应原版 getFiddledDistance
    //六次线性同余叠乘把角点坐标与种子搅在一起 每轴偏移量限制在正负零点四五格内
    private static double FiddledDistance(long seed, int x, int y, int z,
        double distanceX, double distanceY, double distanceZ)
    {
        var value = LinearCongruentialNext(seed, x);
        value = LinearCongruentialNext(value, y);
        value = LinearCongruentialNext(value, z);
        value = LinearCongruentialNext(value, x);
        value = LinearCongruentialNext(value, y);
        value = LinearCongruentialNext(value, z);
        var fiddleX = Fiddle(value);
        var second = LinearCongruentialNext(value, seed);
        var fiddleY = Fiddle(second);
        var fiddleZ = Fiddle(LinearCongruentialNext(second, seed));
        var dz = distanceZ + fiddleZ;
        var dy = distanceY + fiddleY;
        var dx = distanceX + fiddleX;
        return dz * dz + dy * dy + dx * dx;
    }

    //LinearCongruentialNext 一次线性同余推进 对应原版 LinearCongruentialGenerator.next
    //long 溢出即模 2^64 与原版 Java 一致 必须 unchecked
    private static long LinearCongruentialNext(long value, long addend)
        => unchecked(value * (value * 6364136223846793005L + 1442695040888963407L) + addend);

    //Fiddle 取偏移量 对应原版 getFiddle 结果落在正负零点四五格
    private static double Fiddle(long value)
    {
        var uniform = FloorMod(value >> 24, 1024) / 1024.0;
        return (uniform - 0.5) * 0.9;
    }

    //FloorMod 恒非负取模 对应原版 Math.floorMod
    private static long FloorMod(long value, long modulus)
    {
        var result = value % modulus;
        return result < 0 ? result + modulus : result;
    }
}
