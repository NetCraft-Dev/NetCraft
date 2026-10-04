using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Storage;

namespace NetCraft.Game.Server;

//ServerWorldBorderListener 边界变化经玩家集合同步客户端
//对应原版 PlayerList.addWorldborderListener 里挂的那个匿名监听器
//伤害与免伤缓冲是服务端逻辑 不改变客户端状态 空实现
public sealed class ServerWorldBorderListener(PlayerList players) : IBorderChangeListener
{
    public void OnSetSize(WorldBorder border, double newSize)
        => players.BroadcastAll(new ClientboundSetBorderSizePacket(border.GetSize()));

    public void OnLerpSize(WorldBorder border, double fromSize, double targetSize, long ticks, long gameTime)
        => players.BroadcastAll(new ClientboundSetBorderLerpSizePacket(
            border.GetSize(), border.GetLerpTarget(), border.GetLerpTime()));

    public void OnSetCenter(WorldBorder border, double x, double z)
        => players.BroadcastAll(new ClientboundSetBorderCenterPacket(border.CenterX, border.CenterZ));

    public void OnSetWarningTime(WorldBorder border, int time)
        => players.BroadcastAll(new ClientboundSetBorderWarningDelayPacket(border.WarningTime));

    public void OnSetWarningBlocks(WorldBorder border, int blocks)
        => players.BroadcastAll(new ClientboundSetBorderWarningDistancePacket(border.WarningBlocks));

    public void OnSetDamagePerBlock(WorldBorder border, double damagePerBlock) { }

    public void OnSetSafeZone(WorldBorder border, double safeZone) { }
}
