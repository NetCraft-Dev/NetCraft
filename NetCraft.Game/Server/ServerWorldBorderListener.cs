using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Storage;

namespace NetCraft.Game.Server;

//ServerWorldBorderListener syncs border changes to clients through the player set
//maps to the anonymous listener attached in vanilla PlayerList.addWorldborderListener
//Damage and buffer are server logic that do not change client state, so the implementation is empty
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
