namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundPlayerCommandPacket 实体动作包对应原版 ServerboundPlayerCommandPacket
//疾跑潜行起跳落地等实体状态切换 字段 Id(VarInt) Action(VarInt枚举) Data(VarInt)
public sealed record ServerboundPlayerCommandPacket(int Id, PlayerCommandAction Action, int Data)
    : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundPlayerCommandPacket> StreamCodec { get; } = new PlayerCommandCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundPlayerCommand;

    public void Handle(ServerGamePacketListener handler) => handler.HandlePlayerCommand(this);

    private sealed class PlayerCommandCodec : StreamCodec<FriendlyByteBuf, ServerboundPlayerCommandPacket>
    {
        public ServerboundPlayerCommandPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt(), (PlayerCommandAction)buf.ReadVarInt(), buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ServerboundPlayerCommandPacket value)
        {
            buf.WriteVarInt(value.Id);
            buf.WriteVarInt((int)value.Action);
            buf.WriteVarInt(value.Data);
        }
    }
}

//PlayerCommandAction 实体动作枚举声明顺序即网络序号与原版 Action 对齐
public enum PlayerCommandAction
{
    StopSleeping,
    StartSprinting,
    StopSprinting,
    StartRidingJump,
    StopRidingJump,
    OpenInventory,
    StartFallFlying,
}
