using NetCraft.Primitives;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundLevelEventPacket level event packet, maps to vanilla ClientboundLevelEventPacket
//Fields: type Int, pos BlockPos, data Int, globalEvent Boolean
public sealed record ClientboundLevelEventPacket(int Kind, BlockPos Pos, int Data, bool GlobalEvent) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundLevelEventPacket> StreamCodec { get; } = new LevelEventCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundLevelEvent;

    public void Handle(ClientGamePacketListener handler) => handler.HandleLevelEvent(this);

    private sealed class LevelEventCodec : StreamCodec<FriendlyByteBuf, ClientboundLevelEventPacket>
    {
        public ClientboundLevelEventPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadInt(), buf.ReadBlockPos(), buf.ReadInt(), buf.ReadBoolean());

        public void Encode(FriendlyByteBuf buf, ClientboundLevelEventPacket value)
        {
            buf.WriteInt(value.Kind);
            buf.WriteBlockPos(value.Pos);
            buf.WriteInt(value.Data);
            buf.WriteBoolean(value.GlobalEvent);
        }
    }
}
