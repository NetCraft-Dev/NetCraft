namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundSetGameRulePacket set game rule packet, maps to vanilla ServerboundSetGameRulePacket
//Field: Entries(List<Entry>)
public sealed record ServerboundSetGameRulePacket(object Entries) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundSetGameRulePacket> StreamCodec { get; } = new SetGameRuleCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundSetGameRule;

    public void Handle(ServerGamePacketListener handler) => handler.HandleSetGameRule(this);

    private sealed class SetGameRuleCodec : StreamCodec<FriendlyByteBuf, ServerboundSetGameRulePacket>
    {
        public ServerboundSetGameRulePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ServerboundSetGameRulePacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
