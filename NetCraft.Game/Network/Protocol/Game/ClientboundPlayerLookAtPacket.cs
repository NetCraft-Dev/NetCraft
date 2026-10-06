namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundPlayerLookAtPacket player look at packet, maps to vanilla ClientboundPlayerLookAtPacket
//Fields: fromAnchor enum placeholder, x/y/z 3 doubles, atEntity bool, entity VarInt, toAnchor enum placeholder
//When atEntity is false, entity=0 and toAnchor=null
public sealed record ClientboundPlayerLookAtPacket(object FromAnchor, double X, double Y, double Z, bool AtEntity, int Entity, object? ToAnchor) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundPlayerLookAtPacket> StreamCodec { get; } = new PlayerLookAtCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundPlayerLookAt;

    public void Handle(ClientGamePacketListener handler) => handler.HandleLookAt(this);

    private sealed class PlayerLookAtCodec : StreamCodec<FriendlyByteBuf, ClientboundPlayerLookAtPacket>
    {
        public ClientboundPlayerLookAtPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("EntityAnchorArgument.Anchor business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundPlayerLookAtPacket value)
            => throw new NotImplementedException("EntityAnchorArgument.Anchor business type not yet implemented");
    }
}
