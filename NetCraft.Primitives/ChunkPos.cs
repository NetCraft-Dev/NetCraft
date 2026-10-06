namespace NetCraft.Primitives;

//Chunk position, maps to vanilla ChunkPos
//Vanilla is a record, here a readonly struct to reduce allocations
//Only implements the pack and region methods needed by save IO, BlockPos, SectionPos and Codec are deferred
public readonly struct ChunkPos : IEquatable<ChunkPos>
{
    private const int CoordBits = 32;
    private const long CoordMask = 4294967295L;
    private const int RegionBits = 5;
    public const int RegionSize = 32;
    private const int RegionMask = 31;
    public const int RegionMaxIndex = 31;
    private const int HashA = 1664525;
    private const int HashC = 1013904223;
    private const int HashZXor = -559038737;

    public static readonly ChunkPos Zero = new(0, 0);
    public const long InvalidChunkPos = 1875066 | (1875066L << 32);

    public int X { get; }
    public int Z { get; }

    //Block coordinate range covered by the chunk, maps to vanilla getMinBlockX/getMaxBlockX/getMinBlockZ/getMaxBlockZ
    public int MinBlockX => X * 16;
    public int MaxBlockX => MinBlockX + 15;
    public int MinBlockZ => Z * 16;
    public int MaxBlockZ => MinBlockZ + 15;

    public ChunkPos(int x, int z)
    {
        X = x;
        Z = z;
    }

    public long Pack() => Pack(X, Z);

    public static long Pack(int x, int z) => (x & CoordMask) | ((z & CoordMask) << CoordBits);

    public static ChunkPos Unpack(long key) => new((int)key, (int)(key >> CoordBits));

    public static int GetX(long pos) => (int)(pos & CoordMask);

    public static int GetZ(long pos) => (int)((pos >> CoordBits) & CoordMask);

    public int GetRegionX() => X >> RegionBits;

    public int GetRegionZ() => Z >> RegionBits;

    public static int GetRegionX(long pos) => GetX(pos) >> RegionBits;

    public static int GetRegionZ(long pos) => GetZ(pos) >> RegionBits;

    public int GetRegionLocalX() => X & RegionMask;

    public int GetRegionLocalZ() => Z & RegionMask;

    public static ChunkPos MinFromRegion(int regionX, int regionZ)
        => new(regionX << RegionBits, regionZ << RegionBits);

    public static ChunkPos MaxFromRegion(int regionX, int regionZ)
        => new((regionX << RegionBits) + RegionMaxIndex, (regionZ << RegionBits) + RegionMaxIndex);

    public static int Hash(int x, int z)
    {
        int xTransform = (HashA * x) + HashC;
        int zTransform = (HashA * (z ^ HashZXor)) + HashC;
        return xTransform ^ zTransform;
    }

    public override int GetHashCode() => Hash(X, Z);

    public bool Equals(ChunkPos other) => X == other.X && Z == other.Z;

    public override bool Equals(object? obj) => obj is ChunkPos o && Equals(o);

    public static bool operator ==(ChunkPos left, ChunkPos right) => left.Equals(right);

    public static bool operator !=(ChunkPos left, ChunkPos right) => !left.Equals(right);

    public override string ToString() => $"[{X}, {Z}]";

    //Enumerates every chunk position from to, row-major
    public static IEnumerable<ChunkPos> RangeClosed(ChunkPos from, ChunkPos to)
    {
        int xDiff = from.X < to.X ? 1 : -1;
        int zDiff = from.Z < to.Z ? 1 : -1;
        int x = from.X;
        int z = from.Z;
        while (true)
        {
            yield return new ChunkPos(x, z);
            if (x == to.X && z == to.Z) yield break;
            if (x == to.X)
            {
                x = from.X;
                z += zDiff;
            }
            else
            {
                x += xDiff;
            }
        }
    }
}
