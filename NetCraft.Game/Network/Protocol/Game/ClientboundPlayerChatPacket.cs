namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundPlayerChatPacket 玩家聊天包对应原版 ClientboundPlayerChatPacket
//字段 GlobalIndex(int) Sender(UUID) Index(int) Signature(MessageSignature) Body(SignedMessageBody.Packed) UnsignedContent(Component)
public sealed record ClientboundPlayerChatPacket(int GlobalIndex, Guid Sender, int Index, object Signature, object Body, Component UnsignedContent, object FilterMask, object ChatType) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundPlayerChatPacket> StreamCodec { get; } = new PlayerChatCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundPlayerChat;

    public void Handle(ClientGamePacketListener handler) => handler.HandlePlayerChat(this);

    private sealed class PlayerChatCodec : StreamCodec<FriendlyByteBuf, ClientboundPlayerChatPacket>
    {
        public ClientboundPlayerChatPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundPlayerChatPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
