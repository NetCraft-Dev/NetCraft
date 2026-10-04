namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundDisguisedChatPacket 伪装聊天包对应原版 ClientboundDisguisedChatPacket
//字段 Message(Component) ChatType(ChatType.Bound)
public sealed record ClientboundDisguisedChatPacket(Component Message, object ChatType) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundDisguisedChatPacket> StreamCodec { get; } = new DisguisedChatCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundDisguisedChat;

    public void Handle(ClientGamePacketListener handler) => handler.HandleDisguisedChat(this);

    private sealed class DisguisedChatCodec : StreamCodec<FriendlyByteBuf, ClientboundDisguisedChatPacket>
    {
        public ClientboundDisguisedChatPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundDisguisedChatPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
