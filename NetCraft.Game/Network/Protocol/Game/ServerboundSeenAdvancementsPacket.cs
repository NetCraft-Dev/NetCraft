using NetCraft.Registry;

namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundSeenAdvancementsPacket 数据包对应原版 ServerboundSeenAdvancementsPacket
//字段 Action(int 0打开页签1关闭界面) Tab(Identifier 仅打开页签时携带)
public sealed record ServerboundSeenAdvancementsPacket(int Action, Identifier? Tab) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundSeenAdvancementsPacket> StreamCodec { get; } = new SeenAdvancementsCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundSeenAdvancements;

    public void Handle(ServerGamePacketListener handler) => handler.HandleSeenAdvancements(this);

    private sealed class SeenAdvancementsCodec : StreamCodec<FriendlyByteBuf, ServerboundSeenAdvancementsPacket>
    {
        //进度界面打开或关闭时发送 只有 Action=OPENED_TAB 才带页签标识
        public ServerboundSeenAdvancementsPacket Decode(FriendlyByteBuf buf)
        {
            var action = buf.ReadVarInt();
            Identifier? tab = action == 0 ? buf.ReadIdentifier() : null;
            return new(action, tab);
        }

        public void Encode(FriendlyByteBuf buf, ServerboundSeenAdvancementsPacket value)
        {
            buf.WriteVarInt(value.Action);
            if (value.Action == 0) buf.WriteIdentifier(value.Tab!.Value);
        }
    }
}
