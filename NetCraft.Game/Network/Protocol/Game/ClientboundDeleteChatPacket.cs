namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundDeleteChatPacket delete chat packet, maps to vanilla ClientboundDeleteChatPacket
//Field: MessageSignature(MessageSignature.Packed)
public sealed record ClientboundDeleteChatPacket(object MessageSignature) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundDeleteChatPacket> StreamCodec { get; } = new DeleteChatCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundDeleteChat;

    public void Handle(ClientGamePacketListener handler) => handler.HandleDeleteChat(this);

    private sealed class DeleteChatCodec : StreamCodec<FriendlyByteBuf, ClientboundDeleteChatPacket>
    {
        public ClientboundDeleteChatPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundDeleteChatPacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
