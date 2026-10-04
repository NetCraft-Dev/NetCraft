namespace NetCraft.Primitives;

//区块段坐标对应原版net.minecraft.core.SectionPos
//段大小16段内block坐标4位段坐标22+20+22位对应原版位运算
//含blockToSectionCoord/sectionToBlockCoord/asLong/of位运算
public readonly struct SectionPos : IEquatable<SectionPos>
{
    public const int SectionBits = 4;
    public const int SectionSize = 16;
    public const int SectionBlockCount = 4096;
    public const int SectionMask = 15;
    public const int SectionHalfSize = 8;
    public const int SectionMaxIndex = 15;

    private const int PackedXLength = 22;
    private const int PackedYLength = 20;
    private const int PackedZLength = 22;
    private const long PackedXMask = 4194303;
    private const long PackedYMask = 1048575;
    private const long PackedZMask = 4194303;
    private const int YOffset = 0;
    private const int ZOffset = 20;
    private const int XOffset = 42;
    private const int RelativeXShift = 8;
    private const int RelativeYShift = 0;
    private const int RelativeZShift = 4;

    public int X { get; }
    public int Y { get; }
    public int Z { get; }

    public SectionPos(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    //of按x/y/z构造
    public static SectionPos Of(int x, int y, int z) => new(x, y, z);

    //of按BlockPos构造转段坐标
    public static SectionPos Of(BlockPos pos)
        => new(BlockToSectionCoord(pos.X), BlockToSectionCoord(pos.Y), BlockToSectionCoord(pos.Z));

    //of按ChunkPos+sectionY构造
    public static SectionPos Of(ChunkPos chunkPos, int sectionY)
        => new(chunkPos.X, sectionY, chunkPos.Z);

    //of按packed long解压
    public static SectionPos Of(long packed)
        => new(GetX(packed), GetY(packed), GetZ(packed));

    //blockToSectionCoord把block坐标右移4位变段坐标
    public static int BlockToSectionCoord(int blockCoord) => blockCoord >> SectionBits;

    //sectionToBlockCoord把段坐标左移4位并补0变block坐标
    public static int SectionToBlockCoord(int sectionCoord) => sectionCoord << SectionBits;

    //blockToSectionCoord取double版原版用于entity
    public static int BlockToSectionCoord(double blockCoord) => (int)Math.Floor(blockCoord / SectionSize);

    //getX/getY/getZ从packed long取段坐标
    //必须用左移顶到符号位再算术右移还原符号 只做掩码会把负数段坐标(主世界 y 段从 -4 开始)还原成正数
    public static int GetX(long packed) => (int)(packed << (64 - XOffset - PackedXLength) >> (64 - PackedXLength));
    public static int GetY(long packed) => (int)(packed << (64 - YOffset - PackedYLength) >> (64 - PackedYLength));
    public static int GetZ(long packed) => (int)(packed << (64 - ZOffset - PackedZLength) >> (64 - PackedZLength));

    //asLong把段坐标压缩为long对应原版序列化存储
    public long AsLong() => AsLong(X, Y, Z);

    public static long AsLong(int x, int y, int z)
        => ((long)x & PackedXMask) << XOffset
         | ((long)y & PackedYMask) << YOffset
         | ((long)z & PackedZMask) << ZOffset;

    //blockToSection把 packed BlockPos 转 packed SectionPos 光照按 blockNode 找 sectionNode
    public static long BlockToSection(long blockNode)
        => AsLong(
            BlockToSectionCoord(BlockPos.GetX(blockNode)),
            BlockToSectionCoord(BlockPos.GetY(blockNode)),
            BlockToSectionCoord(BlockPos.GetZ(blockNode)));

    //getZeroNode抹掉 y 段只留 x/z 对应原版 column 概念 y 段占低 20 位
    public static long GetZeroNode(long sectionNode) => sectionNode & (-1048576L);

    public static long GetZeroNode(int x, int z) => GetZeroNode(AsLong(x, 0, z));

    //sectionRelative段内 block 相对偏移 0..15
    public static int SectionRelative(int blockCoord) => blockCoord & SectionMask;

    //sectionRelativePos把 BlockPos 压成 12 位段内偏移 x 高位 z 中 y 低位
    public static short SectionRelativePos(BlockPos pos)
        => (short)((SectionRelative(pos.X) << 8) | (SectionRelative(pos.Z) << 4) | SectionRelative(pos.Y));

    //sectionToBlockCoord带段内偏移的重载
    public static int SectionToBlockCoord(int sectionCoord, int offset) => SectionToBlockCoord(sectionCoord) + offset;

    //posToSectionCoord由 double 坐标求段坐标
    public static int PosToSectionCoord(double pos) => BlockToSectionCoord((int)Math.Floor(pos));

    //aroundAndAtBlockPos遍历该 block 及其相邻段 光照 setStoredLevel 用它标记受影响段
    public static void AroundAndAtBlockPos(long blockNode, Action<long> consumer)
        => AroundAndAtBlockPos(BlockPos.GetX(blockNode), BlockPos.GetY(blockNode), BlockPos.GetZ(blockNode), consumer);

    public static void AroundAndAtBlockPos(int blockX, int blockY, int blockZ, Action<long> consumer)
    {
        var minX = BlockToSectionCoord(blockX - 1);
        var maxX = BlockToSectionCoord(blockX + 1);
        var minY = BlockToSectionCoord(blockY - 1);
        var maxY = BlockToSectionCoord(blockY + 1);
        var minZ = BlockToSectionCoord(blockZ - 1);
        var maxZ = BlockToSectionCoord(blockZ + 1);
        if (minX == maxX && minY == maxY && minZ == maxZ)
        {
            consumer(AsLong(minX, minY, minZ));
            return;
        }
        for (var sx = minX; sx <= maxX; sx++)
        for (var sy = minY; sy <= maxY; sy++)
        for (var sz = minZ; sz <= maxZ; sz++)
            consumer(AsLong(sx, sy, sz));
    }

    //offset按方向偏移packed long
    public static long Offset(long packed, Direction direction)
        => Offset(packed, direction.StepX, direction.StepY, direction.StepZ);

    public static long Offset(long packed, int stepX, int stepY, int stepZ)
        => AsLong(GetX(packed) + stepX, GetY(packed) + stepY, GetZ(packed) + stepZ);

    //asBlockPos转BlockPos段左移4位
    public BlockPos AsBlockPos() => new(X << SectionBits, Y << SectionBits, Z << SectionBits);

    //blockToChunk返回段所在ChunkPos
    public ChunkPos AsChunkPos() => new(X, Z);

    //relativeX取段内block相对偏移的X位
    public static int RelativeX(long packed) => (int)(packed >> RelativeXShift) & SectionMask;
    public static int RelativeY(long packed) => (int)(packed >> RelativeYShift) & SectionMask;
    public static int RelativeZ(long packed) => (int)(packed >> RelativeZShift) & SectionMask;

    public override int GetHashCode() => (int)(AsLong() ^ (AsLong() >> 32));

    public bool Equals(SectionPos other) => X == other.X && Y == other.Y && Z == other.Z;

    public override bool Equals(object? obj) => obj is SectionPos s && Equals(s);

    public static bool operator ==(SectionPos left, SectionPos right) => left.Equals(right);
    public static bool operator !=(SectionPos left, SectionPos right) => !left.Equals(right);

    public override string ToString() => $"[{X}, {Y}, {Z}]";
}
