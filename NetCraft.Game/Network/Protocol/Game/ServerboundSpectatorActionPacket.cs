namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundSpectatorActionPacket spectator action packet, maps to vanilla ServerboundSpectatorActionPacket
//Field: SpectateEntityId(OptionalInt, the watched target entity id; empty means stop spectating)
public sealed record ServerboundSpectatorActionPacket(int? SpectateEntityId) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundSpectatorActionPacket> StreamCodec { get; } = new SpectatorActionCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundSpectatorAction;

    public void Handle(ServerGamePacketListener handler) => handler.HandleSpectatorAction(this);

    private sealed class SpectatorActionCodec : StreamCodec<FriendlyByteBuf, ServerboundSpectatorActionPacket>
    {
        //Sent when a spectator switches the followed target; the vanilla OptionalInt writes a presence flag before the varint
        public ServerboundSpectatorActionPacket Decode(FriendlyByteBuf buf)
        {
            if (!buf.ReadBoolean()) return new((int?)null);
            return new(buf.ReadVarInt());
        }

        public void Encode(FriendlyByteBuf buf, ServerboundSpectatorActionPacket value)
        {
            buf.WriteBoolean(value.SpectateEntityId.HasValue);
            if (value.SpectateEntityId.HasValue) buf.WriteVarInt(value.SpectateEntityId.Value);
        }
    }
}
