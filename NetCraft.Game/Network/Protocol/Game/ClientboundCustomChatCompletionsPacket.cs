namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundCustomChatCompletionsPacket 自定义聊天补全包对应原版 ClientboundCustomChatCompletionsPacket
//字段 Action(Action) Entries(List<String>)
public sealed record ClientboundCustomChatCompletionsPacket(object Action, object Entries) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundCustomChatCompletionsPacket> StreamCodec { get; } = new CustomChatCompletionsCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundCustomChatCompletions;

    public void Handle(ClientGamePacketListener handler) => handler.HandleCustomChatCompletions(this);

    private sealed class CustomChatCompletionsCodec : StreamCodec<FriendlyByteBuf, ClientboundCustomChatCompletionsPacket>
    {
        public ClientboundCustomChatCompletionsPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundCustomChatCompletionsPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
