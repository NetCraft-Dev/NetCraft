using System.Security.Cryptography;
using NetCraft.Registry;
using NetCraft.Storage;

namespace NetCraft.Game.World.Level.LevelGen;

//BiomeManager biome manager, maps to vanilla net.minecraft.world.level.biome.BiomeManager
//Finds the nearest quart sample point for a block coordinate, preferring the generated chunk's biome palette and only falling back to full climate sampling out of bounds
//The surface stage queries a biome for every block; vanilla keeps the cost constant through this palette lookup
//Passing BiomeSource as the getter would degenerate into a 6-dimensional climate sample per block and make chunk generation time out
public sealed class BiomeManager
{
    //_chunk the chunk being generated; its biome palette is filled during the BIOMES stage
    private readonly ChunkAccess _chunk;
    //_fallback full sampling when out of bounds; arguments are the block coordinates of the quart, matching those written into the palette during BIOMES
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

    //ObfuscateSeed scrambles the world seed, maps to vanilla obfuscateSeed's sha256(seed).asLong()
    //Vanilla HashCode.asLong reads the first eight bytes little-endian and writes the input little-endian too; match that or every fiddled distance differs
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

    //GetBiome returns the biome of the nearest sample point for a block coordinate, maps to vanilla getBiome
    //Shifts the coordinate two blocks negative then buckets by four, computing a fiddled distance for each of the eight corners and taking the minimum
    //The two-block shift puts the sample at the block centre rather than the edge, and fiddled distance avoids large regular grids
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

    //GetNoiseBiome returns the biome for a quart coordinate, using the palette inside this chunk and falling back to full sampling, maps to vanilla LevelReader.getNoiseBiome
    private Biome GetNoiseBiome(int quartX, int quartY, int quartZ)
    {
        if (quartX >> 2 == _chunk.Pos.X && quartZ >> 2 == _chunk.Pos.Z
            && quartY >= _minQuartY && quartY < _maxQuartY)
            return _chunk.GetNoiseBiome(quartX, quartY, quartZ).Value;
        return _fallback(quartX * 4, quartY * 4, quartZ * 4);
    }

    //FiddledDistance squared distance with a fiddled offset, maps to vanilla getFiddledDistance
    //Six chained linear congruential steps mix the corner coordinate with the seed; each axis offset stays within ±0.45 blocks
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

    //LinearCongruentialNext one linear congruential step, maps to vanilla LinearCongruentialGenerator.next
    //long overflow is modulo 2^64, matching Java vanilla, so unchecked is required
    private static long LinearCongruentialNext(long value, long addend)
        => unchecked(value * (value * 6364136223846793005L + 1442695040888963407L) + addend);

    //Fiddle takes the offset, maps to vanilla getFiddle; the result stays within ±0.45 blocks
    private static double Fiddle(long value)
    {
        var uniform = FloorMod(value >> 24, 1024) / 1024.0;
        return (uniform - 0.5) * 0.9;
    }

    //FloorMod always-non-negative modulo, maps to vanilla Math.floorMod
    private static long FloorMod(long value, long modulus)
    {
        var result = value % modulus;
        return result < 0 ? result + modulus : result;
    }
}
