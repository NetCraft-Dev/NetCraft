namespace NetCraft.Config;
//Global constants (immutable), maps to vanilla net.minecraft.SharedConstants
//Contains the version string, protocol number, world data version and so on
public static class SharedConstants
{
    //Current game version, aligned with vanilla Minecraft 26.2
    public const string Version = "26.2-netcraft";
    //Current protocol version number, 26.x maps to 776
    public const int ProtocolVersion = 776;
    //Hard lower bound of the protocol version, anything below is no longer supported
    public const int ProtocolVersionLowerBound = 754;
    //World data version, drives the DFU migration path
    public const int WorldDataVersion = 4189;
    //Target ticks per second
    public const int TicksPerSecond = 20;
    //Milliseconds per tick, 1000ms divided by 20tps
    public const int MilliPerTick = 1000 / TicksPerSecond;
    //Chunk edge length in blocks
    public const int ChunkSize = 16;
    //Chunk height, 384 since 1.18, covering -64 to 319
    public const int ChunkHeight = 384;
    //Lowest Y coordinate inside a chunk
    public const int MinY = -64;
    //Chunks per edge of a region file (.mca), 32x32 means 1024 chunks
    public const int RegionChunks = 32;
    //Region file sector size, 4096 bytes
    public const int SectorSize = 4096;
    //Max NBT string length, 32767 UTF-8 bytes
    public const int MaxNbtStringLength = 32767;
    //Max NBT nesting depth
    public const int MaxNbtDepth = 512;
    //Overall NBT byte limit, about 2GB by default, adjustable at construction time
    public const long MaxNbtAccounterBytes = 2L * 1024 * 1024 * 1024;
    //Chat formatting prefix code, §, that is U+00A7
    public const char FormattingPrefixCode = '\u00A7';
    //Data version field name in NBT, vanilla SharedConstants.DATA_VERSION_TAG
    public const string DataVersionTag = "DataVersion";

    //Upper bound of chained neighbor updates, maps to vanilla SharedConstants.MAX_CHAINED_NEIGHBOR_UPDATES
    //Updates beyond the limit are dropped, acting as a guard against redstone recursion
    public const int MaxChainedNeighborUpdates = 1000000;
}
