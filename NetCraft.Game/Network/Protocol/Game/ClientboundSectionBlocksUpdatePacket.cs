using NetCraft.Primitives;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSectionBlocksUpdatePacket 区块段方块更新包对应原版 ClientboundSectionBlocksUpdatePacket
//字段 sectionPos SectionPos packedChanges long 数组每项 (stateId<<12)|position 用 VarLong 编解码
public sealed record ClientboundSectionBlocksUpdatePacket(SectionPos SectionPos, long[] PackedChanges) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSectionBlocksUpdatePacket> StreamCodec { get; } = new SectionBlocksUpdateCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSectionBlocksUpdate;

    public void Handle(ClientGamePacketListener handler) => handler.HandleChunkBlocksUpdate(this);

    private sealed class SectionBlocksUpdateCodec : StreamCodec<FriendlyByteBuf, ClientboundSectionBlocksUpdatePacket>
    {
        public ClientboundSectionBlocksUpdatePacket Decode(FriendlyByteBuf buf)
        {
            var sectionPos = buf.ReadSectionPos();
            int count = buf.ReadVarInt();
            long[] changes = new long[count];
            for (int i = 0; i < count; i++)
                changes[i] = buf.ReadVarLong();
            return new(sectionPos, changes);
        }

        public void Encode(FriendlyByteBuf buf, ClientboundSectionBlocksUpdatePacket value)
        {
            buf.WriteSectionPos(value.SectionPos);
            buf.WriteVarInt(value.PackedChanges.Length);
            foreach (long packed in value.PackedChanges)
                buf.WriteVarLong(packed);
        }
    }
}
