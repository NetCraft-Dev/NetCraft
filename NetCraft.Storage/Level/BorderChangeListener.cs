namespace NetCraft.Storage;

//BorderChangeListener 边界变化监听器对应原版 net.minecraft.world.level.border.BorderChangeListener
//服务端用它把边界改动转成网络包广播 存档层只管通知不关心发给谁
public interface IBorderChangeListener
{
    //OnSetSize 边界尺寸直接变化
    void OnSetSize(WorldBorder border, double newSize);

    //OnLerpSize 边界尺寸开始插值变化
    void OnLerpSize(WorldBorder border, double fromSize, double targetSize, long ticks, long gameTime);

    //OnSetCenter 边界中心变化
    void OnSetCenter(WorldBorder border, double x, double z);

    //OnSetWarningTime 边界警告时间变化
    void OnSetWarningTime(WorldBorder border, int time);

    //OnSetWarningBlocks 边界警告距离变化
    void OnSetWarningBlocks(WorldBorder border, int blocks);

    //OnSetDamagePerBlock 每格越界伤害变化
    void OnSetDamagePerBlock(WorldBorder border, double damagePerBlock);

    //OnSetSafeZone 越界免伤缓冲变化
    void OnSetSafeZone(WorldBorder border, double safeZone);
}
