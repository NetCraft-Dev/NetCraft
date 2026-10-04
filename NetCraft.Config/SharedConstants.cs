namespace NetCraft.Config;
//全局常量（不可变）对应原版net.minecraft.SharedConstants
//包含版本号、协议号、世界数据版本等
public static class SharedConstants
{
    //当前游戏版本，与原版Minecraft 26.2对齐
    public const string Version = "26.2-netcraft";
    //当前协议版本号，26.x对应776
    public const int ProtocolVersion = 776;
    //协议版本硬下限，不再向后兼容的下限
    public const int ProtocolVersionLowerBound = 754;
    //世界数据版本，影响DFU迁移路径
    public const int WorldDataVersion = 4189;
    //每秒目标tick数
    public const int TicksPerSecond = 20;
    //单个tick的毫秒数，1000ms除以20tps
    public const int MilliPerTick = 1000 / TicksPerSecond;
    //区块边长，以方块为单位
    public const int ChunkSize = 16;
    //区块高度，1.18+为384，含-64到319
    public const int ChunkHeight = 384;
    //区块内最低Y坐标
    public const int MinY = -64;
    //区域文件.mca单边chunk数，32x32即1024个chunk
    public const int RegionChunks = 32;
    //区域文件sector大小，4096字节
    public const int SectorSize = 4096;
    //NBT字符串最大长度，32767个UTF-8字节
    public const int MaxNbtStringLength = 32767;
    //NBT嵌套深度上限
    public const int MaxNbtDepth = 512;
    //NBT整体字节上限，默认约2GB，可在构造时调整
    public const long MaxNbtAccounterBytes = 2L * 1024 * 1024 * 1024;
    //聊天格式化前缀码，§即U+00A7
    public const char FormattingPrefixCode = '\u00A7';
    //NBT 中数据版本字段名，原版 SharedConstants.DATA_VERSION_TAG
    public const string DataVersionTag = "DataVersion";

    //链式邻居更新上限 对应原版 SharedConstants.MAX_CHAINED_NEIGHBOR_UPDATES
    //超限的更新被丢弃 是红石递归的保护栏
    public const int MaxChainedNeighborUpdates = 1000000;
}