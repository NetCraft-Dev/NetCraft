using NetCraft.Game.World.Level.Chunk;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundChunksBiomesPacket chunk biomes packet, maps to vanilla ClientboundChunksBiomesPacket
//Field: Chunks maps to vanilla chunkBiomeData; each entry is a contiguous run of section biome container bytes
//The client replaces its local biome data wholesale; after a fillbiome command the affected chunks must be resent to refresh coloring
public sealed record ClientboundChunksBiomesPacket(IReadOnlyList<ClientboundChunksBiomesPacket.ChunkBiomeData> Chunks)
    : Packet<ClientGamePacketListener>
{
    //MaxBufferSize per-entry byte cap, aligns with vanilla readByteArray(0x200000)
    private const int MaxBufferSize = 0x200000;

    public static StreamCodec<FriendlyByteBuf, ClientboundChunksBiomesPacket> StreamCodec { get; } = new ChunksBiomesCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundChunksBiomes;

    public void Handle(ClientGamePacketListener handler) => handler.HandleChunksBiomes(this);

    //ForChunks packs the biome data of several chunks into one packet, maps to vanilla forChunks
    public static ClientboundChunksBiomesPacket ForChunks(IReadOnlyList<ChunkAccess> chunks)
    {
        var data = new List<ChunkBiomeData>(chunks.Count);
        foreach (var chunk in chunks) data.Add(new ChunkBiomeData(chunk));
        return new ClientboundChunksBiomesPacket(data);
    }

    //ChunkBiomeData biome data of a single chunk: chunk position + contiguous section biome container bytes
    public sealed record ChunkBiomeData(ChunkPos Pos, byte[] Buffer)
    {
        public ChunkBiomeData(ChunkAccess chunk) : this(chunk.Pos, Extract(chunk)) { }

        //Extract writes the biome container section by section, maps to vanilla extractChunkData
        //A missing section gets a single value palette placeholder so the client can read through by section count in order
        private static byte[] Extract(ChunkAccess chunk)
        {
            var buf = new FriendlyByteBuf();
            for (var i = 0; i < chunk.SectionsCount; i++)
            {
                var section = chunk.GetSection(chunk.MinSectionY + i);
                if (section is null)
                {
                    buf.WriteByte(0);
                    buf.WriteVarInt(Math.Max(0, BuiltInRegistries.BIOME.GetId(Biome.Plains)));
                    continue;
                }
                LevelChunkSerializer.WritePalettedContainer(buf, section.Biomes, LevelChunkSerializer.WriteBiome);
            }
            return buf.ToArray();
        }
    }

    private sealed class ChunksBiomesCodec : StreamCodec<FriendlyByteBuf, ClientboundChunksBiomesPacket>
    {
        //Vanilla order: varint count + each entry (chunk two ints + length-prefixed byte array)
        public ClientboundChunksBiomesPacket Decode(FriendlyByteBuf buf)
        {
            var count = buf.ReadVarInt();
            var list = new List<ChunkBiomeData>(count);
            for (var i = 0; i < count; i++)
            {
                var pos = new ChunkPos(buf.ReadInt(), buf.ReadInt());
                list.Add(new ChunkBiomeData(pos, buf.ReadByteArray(MaxBufferSize)));
            }
            return new ClientboundChunksBiomesPacket(list);
        }

        public void Encode(FriendlyByteBuf buf, ClientboundChunksBiomesPacket value)
        {
            buf.WriteVarInt(value.Chunks.Count);
            foreach (var data in value.Chunks)
            {
                buf.WriteInt(data.Pos.X);
                buf.WriteInt(data.Pos.Z);
                buf.WriteByteArray(data.Buffer);
            }
        }
    }
}
