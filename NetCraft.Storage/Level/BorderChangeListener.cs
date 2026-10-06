namespace NetCraft.Storage;

//BorderChangeListener, border change listener, maps to vanilla net.minecraft.world.level.border.BorderChangeListener
//The server uses it to turn border changes into broadcast packets; the storage layer only notifies and does not care who receives
public interface IBorderChangeListener
{
    //OnSetSize, border size changed directly
    void OnSetSize(WorldBorder border, double newSize);

    //OnLerpSize, border size started interpolating
    void OnLerpSize(WorldBorder border, double fromSize, double targetSize, long ticks, long gameTime);

    //OnSetCenter, border center changed
    void OnSetCenter(WorldBorder border, double x, double z);

    //OnSetWarningTime, border warning time changed
    void OnSetWarningTime(WorldBorder border, int time);

    //OnSetWarningBlocks, border warning distance changed
    void OnSetWarningBlocks(WorldBorder border, int blocks);

    //OnSetDamagePerBlock, out-of-bounds damage per block changed
    void OnSetDamagePerBlock(WorldBorder border, double damagePerBlock);

    //OnSetSafeZone, out-of-bounds safe zone changed
    void OnSetSafeZone(WorldBorder border, double safeZone);
}
