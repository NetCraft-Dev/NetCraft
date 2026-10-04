using NetCraft.Primitives;

namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundPlayerActionPacket 玩家动作包对应原版 ServerboundPlayerActionPacket
//字段 Action(枚举) Pos(BlockPos) Direction(命中面) Sequence(方块变更序号)
public sealed record ServerboundPlayerActionPacket(ServerboundPlayerActionPacket.ActionType Action,
    BlockPos Pos, Direction Direction, int Sequence)
    : Packet<ServerGamePacketListener>
{
    //ActionType 动作枚举 声明顺序即网络序号 与原版 Action 对齐
    public enum ActionType
    {
        StartDestroyBlock,
        AbortDestroyBlock,
        StopDestroyBlock,
        DropAllItems,
        DropItem,
        ReleaseUseItem,
        SwapItemWithOffhand,
        Stab,
    }

    public static StreamCodec<FriendlyByteBuf, ServerboundPlayerActionPacket> StreamCodec { get; } = new PlayerActionCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundPlayerAction;

    public void Handle(ServerGamePacketListener handler) => handler.HandlePlayerAction(this);

    private sealed class PlayerActionCodec : StreamCodec<FriendlyByteBuf, ServerboundPlayerActionPacket>
    {
        public ServerboundPlayerActionPacket Decode(FriendlyByteBuf buf)
        {
            var action = buf.ReadEnum<ActionType>();
            var pos = buf.ReadBlockPos();
            var direction = Direction.ById(buf.ReadUnsignedByte());
            return new ServerboundPlayerActionPacket(action, pos, direction, buf.ReadVarInt());
        }

        public void Encode(FriendlyByteBuf buf, ServerboundPlayerActionPacket value)
        {
            buf.WriteEnum(value.Action);
            buf.WriteBlockPos(value.Pos);
            buf.WriteByte((byte)value.Direction.Id3D);
            buf.WriteVarInt(value.Sequence);
        }
    }
}
