namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundClientCommandPacket 客户端命令包对应原版 ServerboundClientCommandPacket
//字段 Action(ClientCommandAction) 如请求重生/请求统计/请求游戏规则值
public sealed record ServerboundClientCommandPacket(ClientCommandAction Action) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundClientCommandPacket> StreamCodec { get; } = new ClientCommandCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundClientCommand;

    public void Handle(ServerGamePacketListener handler) => handler.HandleClientCommand(this);

    private sealed class ClientCommandCodec : StreamCodec<FriendlyByteBuf, ServerboundClientCommandPacket>
    {
        //原版 writeEnum(action) 即 VarInt 枚举序号
        public ServerboundClientCommandPacket Decode(FriendlyByteBuf buf)
            => new((ClientCommandAction)buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ServerboundClientCommandPacket value)
            => buf.WriteVarInt((int)value.Action);
    }
}

//ClientCommandAction 客户端命令动作对应原版 ServerboundClientCommandPacket.Action
public enum ClientCommandAction
{
    PerformRespawn,
    RequestStats,
    RequestGameRuleValues
}
