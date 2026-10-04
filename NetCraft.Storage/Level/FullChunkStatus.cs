namespace NetCraft.Storage;

//FullChunkStatus 区块加载档位对应原版 net.minecraft.server.level.FullChunkStatus
//声明顺序即强弱 Inaccessible 最弱 EntityTicking 最强 比较时直接用序号
public enum FullChunkStatus
{
    Inaccessible,
    Full,
    BlockTicking,
    EntityTicking,
}

//FullChunkStatusExtensions 加载档位比较
public static class FullChunkStatusExtensions
{
    //IsOrAfter 是否达到至少 other 这一档 对应原版 isOrAfter
    public static bool IsOrAfter(this FullChunkStatus status, FullChunkStatus other) => status >= other;
}
