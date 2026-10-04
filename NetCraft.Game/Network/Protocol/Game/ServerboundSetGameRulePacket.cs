namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundSetGameRulePacket 数据包对应原版 ServerboundSetGameRulePacket
//字段 Entries(List<Entry>)
public sealed record ServerboundSetGameRulePacket(object Entries) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundSetGameRulePacket> StreamCodec { get; } = new SetGameRuleCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundSetGameRule;

    public void Handle(ServerGamePacketListener handler) => handler.HandleSetGameRule(this);

    private sealed class SetGameRuleCodec : StreamCodec<FriendlyByteBuf, ServerboundSetGameRulePacket>
    {
        public ServerboundSetGameRulePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundSetGameRulePacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
