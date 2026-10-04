namespace NetCraft.Primitives;

//方块位置对应原版net.minecraft.core.BlockPos
//原版继承Vec3i这里readonly struct不能继承所以直接持有X/Y/Z字段并复用Vec3i方法
//含asLong/getX/getY/getZ位运算pack对应原版压缩存储
public readonly struct BlockPos : IEquatable<BlockPos>
{
    public static readonly BlockPos Zero = new(0, 0, 0);

    //PACKED_HORIZONTAL_LENGTH原版依赖Level.MAX_LEVEL_SIZE这里固定为26对齐原版默认值
    public const int PackedHorizontalLength = 26;
    public const int PackedYLength = 64 - 2 * PackedHorizontalLength;
    private const long PackedXMask = (1 << PackedHorizontalLength) - 1;
    private const long PackedYMask = (1 << PackedYLength) - 1;
    private const long PackedZMask = (1 << PackedHorizontalLength) - 1;
    private const int ZOffset = PackedYLength;
    private const int XOffset = PackedYLength + PackedHorizontalLength;
    public const int MaxHorizontalCoordinate = (1 << PackedHorizontalLength) / 2 - 1;

    public int X { get; }
    public int Y { get; }
    public int Z { get; }

    public BlockPos(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public BlockPos(Vec3i vec) : this(vec.X, vec.Y, vec.Z) { }

    //asVec3i转Vec3i
    public Vec3i AsVec3i() => new(X, Y, Z);

    //asLong把BlockPos压缩为long对应原版序列化存储
    public long AsLong()
    {
        long x = X & PackedXMask;
        long y = Y & PackedYMask;
        long z = Z & PackedZMask;
        return (y << 0) | (z << ZOffset) | (x << XOffset);
    }

    //fromLong从long解压为BlockPos
    public static BlockPos FromLong(long packed)
    {
        int x = (int)((packed << (64 - XOffset - PackedHorizontalLength)) >> (64 - PackedHorizontalLength));
        int y = (int)((packed << (64 - PackedYLength)) >> (64 - PackedYLength));
        int z = (int)((packed << (64 - ZOffset - PackedHorizontalLength)) >> (64 - PackedHorizontalLength));
        return new BlockPos(x, y, z);
    }

    //getX从packed取X
    public static int GetX(long packed)
        => (int)((packed << (64 - XOffset - PackedHorizontalLength)) >> (64 - PackedHorizontalLength));

    public static int GetY(long packed)
        => (int)((packed << (64 - PackedYLength)) >> (64 - PackedYLength));

    public static int GetZ(long packed)
        => (int)((packed << (64 - ZOffset - PackedHorizontalLength)) >> (64 - PackedHorizontalLength));

    //offset按方向偏移返回新BlockPos
    public BlockPos Offset(Direction direction) => new(X + direction.StepX, Y + direction.StepY, Z + direction.StepZ);

    public BlockPos Offset(int x, int y, int z)
        => (x == 0 && y == 0 && z == 0) ? this : new BlockPos(X + x, Y + y, Z + z);

    public BlockPos Offset(Vec3i vec) => Offset(vec.X, vec.Y, vec.Z);

    public BlockPos Relative(Direction direction, int steps)
        => steps == 0 ? this : new BlockPos(X + direction.StepX * steps, Y + direction.StepY * steps, Z + direction.StepZ * steps);

    //offsetPacked静态偏移packed long对应原版offset(long, Direction)
    public static long Offset(long packed, Direction direction)
        => Offset(packed, direction.StepX, direction.StepY, direction.StepZ);

    public static long Offset(long packed, int stepX, int stepY, int stepZ)
        => AsLong(GetX(packed) + stepX, GetY(packed) + stepY, GetZ(packed) + stepZ);

    //asLong静态构造packed long对应原版asLong
    public static long AsLong(int x, int y, int z)
    {
        long xPacked = x & PackedXMask;
        long yPacked = y & PackedYMask;
        long zPacked = z & PackedZMask;
        return (yPacked << 0) | (zPacked << ZOffset) | (xPacked << XOffset);
    }

    //getFlatIndex 清掉低 4 位把 y 对齐到区段底部对应原版 getFlatIndex
    public static long GetFlatIndex(long packed) => packed & -16;

    public override int GetHashCode() => (int)(AsLong() ^ (AsLong() >> 32));

    public bool Equals(BlockPos other) => X == other.X && Y == other.Y && Z == other.Z;

    public override bool Equals(object? obj) => obj is BlockPos b && Equals(b);

    public static bool operator ==(BlockPos left, BlockPos right) => left.Equals(right);
    public static bool operator !=(BlockPos left, BlockPos right) => !left.Equals(right);

    public override string ToString() => $"[{X}, {Y}, {Z}]";
}
