namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundCustomChatCompletionsPacket custom chat completions packet, maps to vanilla ClientboundCustomChatCompletionsPacket
//Fields: Action(Action), Entries(List<String>)
public sealed record ClientboundCustomChatCompletionsPacket(object Action, object Entries) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundCustomChatCompletionsPacket> StreamCodec { get; } = new CustomChatCompletionsCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundCustomChatCompletions;

    public void Handle(ClientGamePacketListener handler) => handler.HandleCustomChatCompletions(this);

    private sealed class CustomChatCompletionsCodec : StreamCodec<FriendlyByteBuf, ClientboundCustomChatCompletionsPacket>
    {
        public ClientboundCustomChatCompletionsPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundCustomChatCompletionsPacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
