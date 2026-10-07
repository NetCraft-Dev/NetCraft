namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundChatCommandPacket player executing a slash command, maps to vanilla ServerboundChatCommandPacket
//26.2 removed command signatures, leaving only Command(String); signed commands go through the separate chat_command_signed packet
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
