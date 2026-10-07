namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundRenameItemPacket rename item packet, maps to vanilla ServerboundRenameItemPacket
//Field: Name(String)
public sealed record ServerboundRenameItemPacket(string Name) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundRenameItemPacket> StreamCodec { get; } = new RenameItemCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundRenameItem;

    public void Handle(ServerGamePacketListener handler) => handler.HandleRenameItem(this);

    private sealed class RenameItemCodec : StreamCodec<FriendlyByteBuf, ServerboundRenameItemPacket>
    {
        //Sent when submitting in the anvil rename screen, carrying only a single new name string
        public ServerboundRenameItemPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadString());

        public void Encode(FriendlyByteBuf buf, ServerboundRenameItemPacket value)
            => buf.WriteString(value.Name);
    }
}
