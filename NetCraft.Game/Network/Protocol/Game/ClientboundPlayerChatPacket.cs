namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundPlayerChatPacket player chat packet, maps to vanilla ClientboundPlayerChatPacket
//Fields: GlobalIndex(int), Sender(UUID), Index(int), Signature(MessageSignature), Body(SignedMessageBody.Packed), UnsignedContent(Component)
public sealed record ClientboundPlayerChatPacket(int GlobalIndex, Guid Sender, int Index, object Signature, object Body, Component UnsignedContent, object FilterMask, object ChatType) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundPlayerChatPacket> StreamCodec { get; } = new PlayerChatCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundPlayerChat;

    public void Handle(ClientGamePacketListener handler) => handler.HandlePlayerChat(this);

    private sealed class PlayerChatCodec : StreamCodec<FriendlyByteBuf, ClientboundPlayerChatPacket>
    {
        public ClientboundPlayerChatPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundPlayerChatPacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
