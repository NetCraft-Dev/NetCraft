namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundDisguisedChatPacket disguised chat packet, maps to vanilla ClientboundDisguisedChatPacket
//Fields: Message(Component), ChatType(ChatType.Bound)
public sealed record ClientboundDisguisedChatPacket(Component Message, object ChatType) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundDisguisedChatPacket> StreamCodec { get; } = new DisguisedChatCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundDisguisedChat;

    public void Handle(ClientGamePacketListener handler) => handler.HandleDisguisedChat(this);

    private sealed class DisguisedChatCodec : StreamCodec<FriendlyByteBuf, ClientboundDisguisedChatPacket>
    {
        public ClientboundDisguisedChatPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundDisguisedChatPacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
