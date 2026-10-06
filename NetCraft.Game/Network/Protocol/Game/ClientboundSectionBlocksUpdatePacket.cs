using NetCraft.Primitives;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSectionBlocksUpdatePacket section blocks update packet, maps to vanilla ClientboundSectionBlocksUpdatePacket
//Fields: sectionPos SectionPos, packedChanges long array where each entry is (stateId<<12)|position, encoded as VarLong
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
