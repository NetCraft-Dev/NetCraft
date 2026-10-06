namespace NetCraft.Primitives;

//Section position, maps to vanilla net.minecraft.core.SectionPos
//Section size 16, in-section block coordinates take 4 bits, section coordinates use 22+20+22 bits, maps to the vanilla bitwise packing
//Includes blockToSectionCoord/sectionToBlockCoord/asLong/of bitwise operations
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

    //of constructs from x/y/z
    public static SectionPos Of(int x, int y, int z) => new(x, y, z);

    //of constructs from a BlockPos, converting to section coordinates
    public static SectionPos Of(BlockPos pos)
        => new(BlockToSectionCoord(pos.X), BlockToSectionCoord(pos.Y), BlockToSectionCoord(pos.Z));

    //of constructs from a ChunkPos + sectionY
    public static SectionPos Of(ChunkPos chunkPos, int sectionY)
        => new(chunkPos.X, sectionY, chunkPos.Z);

    //of unpacks from a packed long
    public static SectionPos Of(long packed)
        => new(GetX(packed), GetY(packed), GetZ(packed));

    //blockToSectionCoord shifts block coordinates right by 4 bits into section coordinates
    public static int BlockToSectionCoord(int blockCoord) => blockCoord >> SectionBits;

    //sectionToBlockCoord shifts section coordinates left by 4 bits and zero-fills into block coordinates
    public static int SectionToBlockCoord(int sectionCoord) => sectionCoord << SectionBits;

    //blockToSectionCoord double version, vanilla uses it for entities
    public static int BlockToSectionCoord(double blockCoord) => (int)Math.Floor(blockCoord / SectionSize);

    //getX/getY/getZ read section coordinates from a packed long
    //Must shift left to the sign bit then arithmetic shift right to restore the sign, masking alone would turn negative section coordinates (overworld y sections start at -4) into positive ones
    public static int GetX(long packed) => (int)(packed << (64 - XOffset - PackedXLength) >> (64 - PackedXLength));
    public static int GetY(long packed) => (int)(packed << (64 - YOffset - PackedYLength) >> (64 - PackedYLength));
    public static int GetZ(long packed) => (int)(packed << (64 - ZOffset - PackedZLength) >> (64 - PackedZLength));

    //asLong packs section coordinates into a long, maps to the vanilla serialized storage
    public long AsLong() => AsLong(X, Y, Z);

    public static long AsLong(int x, int y, int z)
        => ((long)x & PackedXMask) << XOffset
         | ((long)y & PackedYMask) << YOffset
         | ((long)z & PackedZMask) << ZOffset;

    //blockToSection converts a packed BlockPos to a packed SectionPos, lighting uses it to find the sectionNode from a blockNode
    public static long BlockToSection(long blockNode)
        => AsLong(
            BlockToSectionCoord(BlockPos.GetX(blockNode)),
            BlockToSectionCoord(BlockPos.GetY(blockNode)),
            BlockToSectionCoord(BlockPos.GetZ(blockNode)));

    //getZeroNode clears the y section leaving only x/z, maps to the vanilla column concept where the y section occupies the low 20 bits
    public static long GetZeroNode(long sectionNode) => sectionNode & (-1048576L);

    public static long GetZeroNode(int x, int z) => GetZeroNode(AsLong(x, 0, z));

    //sectionRelative block-relative offset within the section, 0..15
    public static int SectionRelative(int blockCoord) => blockCoord & SectionMask;

    //sectionRelativePos packs a BlockPos into a 12-bit in-section offset, x high, z middle, y low
    public static short SectionRelativePos(BlockPos pos)
        => (short)((SectionRelative(pos.X) << 8) | (SectionRelative(pos.Z) << 4) | SectionRelative(pos.Y));

    //sectionToBlockCoord overload with an in-section offset
    public static int SectionToBlockCoord(int sectionCoord, int offset) => SectionToBlockCoord(sectionCoord) + offset;

    //posToSectionCoord derives section coordinates from a double coordinate
    public static int PosToSectionCoord(double pos) => BlockToSectionCoord((int)Math.Floor(pos));

    //aroundAndAtBlockPos iterates the block and its neighboring sections, lighting setStoredLevel uses it to mark affected sections
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

    //offset offsets a packed long along a direction
    public static long Offset(long packed, Direction direction)
        => Offset(packed, direction.StepX, direction.StepY, direction.StepZ);

    public static long Offset(long packed, int stepX, int stepY, int stepZ)
        => AsLong(GetX(packed) + stepX, GetY(packed) + stepY, GetZ(packed) + stepZ);

    //asBlockPos converts to a BlockPos, shifting the section left by 4 bits
    public BlockPos AsBlockPos() => new(X << SectionBits, Y << SectionBits, Z << SectionBits);

    //blockToChunk returns the ChunkPos containing the section
    public ChunkPos AsChunkPos() => new(X, Z);

    //relativeX reads the X part of the in-section block-relative offset
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
