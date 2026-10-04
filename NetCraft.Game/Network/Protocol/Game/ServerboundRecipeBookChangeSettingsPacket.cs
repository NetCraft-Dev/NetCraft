namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundRecipeBookChangeSettingsPacket 数据包对应原版 ServerboundRecipeBookChangeSettingsPacket
//字段 BookType(原版 RecipeBookType 枚举序号) IsOpen(boolean) IsFiltering(boolean)
//nc 尚无配方书系统 BookType 存序号 监听器侧空实现
public sealed record ServerboundRecipeBookChangeSettingsPacket(int BookType, bool IsOpen, bool IsFiltering) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundRecipeBookChangeSettingsPacket> StreamCodec { get; } = new RecipeBookChangeSettingsCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundRecipeBookChangeSettings;

    public void Handle(ServerGamePacketListener handler) => handler.HandleRecipeBookChangeSettingsPacket(this);

    private sealed class RecipeBookChangeSettingsCodec : StreamCodec<FriendlyByteBuf, ServerboundRecipeBookChangeSettingsPacket>
    {
        //原版是 composite(RecipeBookType 的 varint 序号, bool, bool)
        //打开或关闭配方书界面时客户端都会发 不注册会在服务端日志里刷未知包告警
        public ServerboundRecipeBookChangeSettingsPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt(), buf.ReadBoolean(), buf.ReadBoolean());

        public void Encode(FriendlyByteBuf buf, ServerboundRecipeBookChangeSettingsPacket value)
        {
            buf.WriteVarInt(value.BookType);
            buf.WriteBoolean(value.IsOpen);
            buf.WriteBoolean(value.IsFiltering);
        }
    }
}
