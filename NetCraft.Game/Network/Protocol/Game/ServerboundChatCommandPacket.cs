namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundChatCommandPacket 玩家执行斜杠命令对应原版 ServerboundChatCommandPacket
//26.2 移除了命令签名 字段只剩 Command(String) 带签名走独立的 chat_command_signed 包
public sealed record ServerboundChatCommandPacket(string Command) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundChatCommandPacket> StreamCodec { get; } = new ChatCommandCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundChatCommand;

    public void Handle(ServerGamePacketListener handler) => handler.HandleChatCommand(this);

    private sealed class ChatCommandCodec : StreamCodec<FriendlyByteBuf, ServerboundChatCommandPacket>
    {
        public ServerboundChatCommandPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadString());

        public void Encode(FriendlyByteBuf buf, ServerboundChatCommandPacket value)
            => buf.WriteString(value.Command);
    }
}
