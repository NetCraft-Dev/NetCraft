namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundResetScorePacket reset score packet, maps to vanilla ClientboundResetScorePacket
//Fields: Owner(String), ObjectiveName(String)
public sealed record ClientboundResetScorePacket(string Owner, string ObjectiveName) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundResetScorePacket> StreamCodec { get; } = new ResetScoreCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundResetScore;

    public void Handle(ClientGamePacketListener handler) => handler.HandleResetScore(this);

    private sealed class ResetScoreCodec : StreamCodec<FriendlyByteBuf, ClientboundResetScorePacket>
    {
        public ClientboundResetScorePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundResetScorePacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
