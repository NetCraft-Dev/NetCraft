namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetScorePacket set score packet, maps to vanilla ClientboundSetScorePacket
//Fields: Owner(String), ObjectiveName(String), Score(int), Display(Optional<Component>), NumberFormat(Optional<NumberFormat>)
public sealed record ClientboundSetScorePacket(string Owner, string ObjectiveName, int Score, object Display, object NumberFormat) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetScorePacket> StreamCodec { get; } = new SetScoreCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetScore;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetScore(this);

    private sealed class SetScoreCodec : StreamCodec<FriendlyByteBuf, ClientboundSetScorePacket>
    {
        public ClientboundSetScorePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundSetScorePacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
