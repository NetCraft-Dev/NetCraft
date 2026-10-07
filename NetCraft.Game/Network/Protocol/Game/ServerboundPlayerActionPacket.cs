using NetCraft.Primitives;

namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundPlayerActionPacket player action packet, maps to vanilla ServerboundPlayerActionPacket
//Fields: Action(enum), Pos(BlockPos), Direction(hit face), Sequence(block change sequence)
public sealed record ServerboundPlayerActionPacket(ServerboundPlayerActionPacket.ActionType Action,
    BlockPos Pos, Direction Direction, int Sequence)
    : Packet<ServerGamePacketListener>
{
    //ActionType action enum, declaration order is the network ordinal, aligns with vanilla Action
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
