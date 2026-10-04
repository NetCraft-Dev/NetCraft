namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetPlayerTeamPacket 玩家队伍包对应原版 ClientboundSetPlayerTeamPacket
//字段 Method(int) Name(String) Players(Collection<String>) Parameters(Optional<Parameters>)
public sealed record ClientboundSetPlayerTeamPacket(int Method, string Name, object Players, object Parameters) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetPlayerTeamPacket> StreamCodec { get; } = new SetPlayerTeamCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetPlayerTeam;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetPlayerTeamPacket(this);

    private sealed class SetPlayerTeamCodec : StreamCodec<FriendlyByteBuf, ClientboundSetPlayerTeamPacket>
    {
        public ClientboundSetPlayerTeamPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundSetPlayerTeamPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
