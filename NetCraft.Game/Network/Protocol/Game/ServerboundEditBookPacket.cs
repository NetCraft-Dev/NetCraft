using System.Collections.Generic;

namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundEditBookPacket 数据包对应原版 ServerboundEditBookPacket
//字段 Slot(int) Pages(List<string> 每页上限 1024) Title(string 可为空 上限 32)
public sealed record ServerboundEditBookPacket(int Slot, List<string> Pages, string? Title) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundEditBookPacket> StreamCodec { get; } = new EditBookCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundEditBook;

    public void Handle(ServerGamePacketListener handler) => handler.HandleEditBook(this);

    private sealed class EditBookCodec : StreamCodec<FriendlyByteBuf, ServerboundEditBookPacket>
    {
        //书与笔界面保存时发送 页列表后跟可选标题
        public ServerboundEditBookPacket Decode(FriendlyByteBuf buf)
        {
            var slot = buf.ReadVarInt();
            var count = buf.ReadVarInt();
            var pages = new List<string>(count);
            for (var i = 0; i < count; i++) pages.Add(buf.ReadString(1024));
            string? title = buf.ReadBoolean() ? buf.ReadString(32) : null;
            return new(slot, pages, title);
        }

        public void Encode(FriendlyByteBuf buf, ServerboundEditBookPacket value)
        {
            buf.WriteVarInt(value.Slot);
            buf.WriteVarInt(value.Pages.Count);
            foreach (var page in value.Pages) buf.WriteString(page, 1024);
            buf.WriteBoolean(value.Title != null);
            if (value.Title != null) buf.WriteString(value.Title, 32);
        }
    }
}
