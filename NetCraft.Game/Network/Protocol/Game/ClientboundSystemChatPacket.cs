namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSystemChatPacket 系统聊天包对应原版 ClientboundSystemChatPacket
//字段 Content(Component) Overlay(boolean)
public sealed record ClientboundSystemChatPacket(Component Content, bool Overlay) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSystemChatPacket> StreamCodec { get; } = new SystemChatCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSystemChat;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSystemChat(this);

    private sealed class SystemChatCodec : StreamCodec<FriendlyByteBuf, ClientboundSystemChatPacket>
    {
        public ClientboundSystemChatPacket Decode(FriendlyByteBuf buf)
            => new(ComponentSerialization.StreamCodec.Decode(buf), buf.ReadBoolean());

        public void Encode(FriendlyByteBuf buf, ClientboundSystemChatPacket value)
        {
            ComponentSerialization.StreamCodec.Encode(buf, value.Content);
            buf.WriteBoolean(value.Overlay);
        }
    }
}
