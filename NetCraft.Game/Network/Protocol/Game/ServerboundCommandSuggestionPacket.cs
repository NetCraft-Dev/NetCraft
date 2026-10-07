namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundCommandSuggestionPacket suggestion request packet, maps to vanilla ServerboundCommandSuggestionPacket
//Fields: Id(int), Command(String)
public sealed record ServerboundCommandSuggestionPacket(int Id, string Command) : Packet<ServerGamePacketListener>
{
    //MaxCommandLength vanilla readUtf(32500)
    public const int MaxCommandLength = 32500;

    public static StreamCodec<FriendlyByteBuf, ServerboundCommandSuggestionPacket> StreamCodec { get; } = new CommandSuggestionCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundCommandSuggestion;

    public void Handle(ServerGamePacketListener handler) => handler.HandleCustomCommandSuggestions(this);

    private sealed class CommandSuggestionCodec : StreamCodec<FriendlyByteBuf, ServerboundCommandSuggestionPacket>
    {
        //Vanilla write order: writeVarInt(id), writeUtf(command,32500)
        public ServerboundCommandSuggestionPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt(), buf.ReadString(MaxCommandLength));

        public void Encode(FriendlyByteBuf buf, ServerboundCommandSuggestionPacket value)
        {
            buf.WriteVarInt(value.Id);
            buf.WriteString(value.Command, MaxCommandLength);
        }
    }
}
