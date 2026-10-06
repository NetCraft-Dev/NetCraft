namespace NetCraft.Storage;

//FullChunkStatus, chunk load tier, maps to vanilla net.minecraft.server.level.FullChunkStatus
//Declaration order is strength order: Inaccessible weakest, EntityTicking strongest; comparisons use the ordinal
public enum FullChunkStatus
{
    Inaccessible,
    Full,
    BlockTicking,
    EntityTicking,
}

//FullChunkStatusExtensions, load tier comparison
public static class FullChunkStatusExtensions
{
    //IsOrAfter, whether at least the other tier is reached, maps to vanilla isOrAfter
    public static bool IsOrAfter(this FullChunkStatus status, FullChunkStatus other) => status >= other;
}
