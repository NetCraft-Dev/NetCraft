namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundPlayerLookAtPacket 玩家注视包对应原版 ClientboundPlayerLookAtPacket
//字段 fromAnchor enum 占位 x/y/z 3 double atEntity bool entity VarInt toAnchor enum 占位
//atEntity 为 false 时 entity=0 toAnchor=null
public sealed record ClientboundPlayerLookAtPacket(object FromAnchor, double X, double Y, double Z, bool AtEntity, int Entity, object? ToAnchor) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundPlayerLookAtPacket> StreamCodec { get; } = new PlayerLookAtCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundPlayerLookAt;

    public void Handle(ClientGamePacketListener handler) => handler.HandleLookAt(this);

    private sealed class PlayerLookAtCodec : StreamCodec<FriendlyByteBuf, ClientboundPlayerLookAtPacket>
    {
        public ClientboundPlayerLookAtPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("EntityAnchorArgument.Anchor 业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundPlayerLookAtPacket value)
            => throw new NotImplementedException("EntityAnchorArgument.Anchor 业务类型待实现");
    }
}
