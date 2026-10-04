namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundDeleteChatPacket 删除聊天包对应原版 ClientboundDeleteChatPacket
//字段 MessageSignature(MessageSignature.Packed)
public sealed record ClientboundDeleteChatPacket(object MessageSignature) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundDeleteChatPacket> StreamCodec { get; } = new DeleteChatCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundDeleteChat;

    public void Handle(ClientGamePacketListener handler) => handler.HandleDeleteChat(this);

    private sealed class DeleteChatCodec : StreamCodec<FriendlyByteBuf, ClientboundDeleteChatPacket>
    {
        public ClientboundDeleteChatPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundDeleteChatPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
