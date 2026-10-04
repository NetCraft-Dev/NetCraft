namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundRenameItemPacket 数据包对应原版 ServerboundRenameItemPacket
//字段 Name(String)
public sealed record ServerboundRenameItemPacket(string Name) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundRenameItemPacket> StreamCodec { get; } = new RenameItemCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundRenameItem;

    public void Handle(ServerGamePacketListener handler) => handler.HandleRenameItem(this);

    private sealed class RenameItemCodec : StreamCodec<FriendlyByteBuf, ServerboundRenameItemPacket>
    {
        //铁砧改名界面提交时发送 只有一个新名称字符串
        public ServerboundRenameItemPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadString());

        public void Encode(FriendlyByteBuf buf, ServerboundRenameItemPacket value)
            => buf.WriteString(value.Name);
    }
}
