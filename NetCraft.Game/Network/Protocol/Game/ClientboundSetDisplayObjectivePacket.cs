namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetDisplayObjectivePacket display objective packet, maps to vanilla ClientboundSetDisplayObjectivePacket
//Fields: Slot(DisplaySlot), ObjectiveName(String)
public sealed record ClientboundSetDisplayObjectivePacket(object Slot, string ObjectiveName) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetDisplayObjectivePacket> StreamCodec { get; } = new SetDisplayObjectiveCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetDisplayObjective;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetDisplayObjective(this);

    private sealed class SetDisplayObjectiveCodec : StreamCodec<FriendlyByteBuf, ClientboundSetDisplayObjectivePacket>
    {
        public ClientboundSetDisplayObjectivePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundSetDisplayObjectivePacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
