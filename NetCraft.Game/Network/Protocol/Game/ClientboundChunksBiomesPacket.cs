using NetCraft.Game.World.Level.Chunk;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundChunksBiomesPacket 区块生物群系包对应原版 ClientboundChunksBiomesPacket
//字段 Chunks 对应原版 chunkBiomeData 每项是一段连续的区段 biome 容器字节
//客户端整块替换本地 biome 数据 fillbiome 命令执行后需要重发受影响的区块才会刷新配色
public sealed record ClientboundChunksBiomesPacket(IReadOnlyList<ClientboundChunksBiomesPacket.ChunkBiomeData> Chunks)
    : Packet<ClientGamePacketListener>
{
    //MaxBufferSize 单项字节上限 对齐原版 readByteArray(0x200000)
    private const int MaxBufferSize = 0x200000;

    public static StreamCodec<FriendlyByteBuf, ClientboundChunksBiomesPacket> StreamCodec { get; } = new ChunksBiomesCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundChunksBiomes;

    public void Handle(ClientGamePacketListener handler) => handler.HandleChunksBiomes(this);

    //ForChunks 把若干区块的 biome 数据打包成一个包 对应原版 forChunks
    public static ClientboundChunksBiomesPacket ForChunks(IReadOnlyList<ChunkAccess> chunks)
    {
        var data = new List<ChunkBiomeData>(chunks.Count);
        foreach (var chunk in chunks) data.Add(new ChunkBiomeData(chunk));
        return new ClientboundChunksBiomesPacket(data);
    }

    //ChunkBiomeData 单区块 biome 数据 区块位置 + 各区段 biome 容器连续字节
    public sealed record ChunkBiomeData(ChunkPos Pos, byte[] Buffer)
    {
        public ChunkBiomeData(ChunkAccess chunk) : this(chunk.Pos, Extract(chunk)) { }

        //Extract 逐区段写 biome 容器 对应原版 extractChunkData
        //缺失区段补一个 single value palette 占位 保证客户端按区段数顺序读到底
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
        //原版顺序 varint 数量 + 每项(区块两 int + 长度前缀字节数组)
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
