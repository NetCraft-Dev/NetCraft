namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetPlayerTeamPacket player team packet, maps to vanilla ClientboundSetPlayerTeamPacket
//Fields: Method(int), Name(String), Players(Collection<String>), Parameters(Optional<Parameters>)
public sealed record ClientboundSetPlayerTeamPacket(int Method, string Name, object Players, object Parameters) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetPlayerTeamPacket> StreamCodec { get; } = new SetPlayerTeamCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetPlayerTeam;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetPlayerTeamPacket(this);

    private sealed class SetPlayerTeamCodec : StreamCodec<FriendlyByteBuf, ClientboundSetPlayerTeamPacket>
    {
        public ClientboundSetPlayerTeamPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundSetPlayerTeamPacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
