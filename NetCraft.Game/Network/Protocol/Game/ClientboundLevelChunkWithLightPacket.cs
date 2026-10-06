using NetCraft.Game.Bootstrap;
using NetCraft.Game.World.Level.Chunk;
using NetCraft.Nbt;
using NetCraft.Network;
using NetCraft.Primitives;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;
using NetCraft.Storage.Paletted;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundLevelChunkWithLightPacket level chunk with light packet, maps to vanilla ClientboundLevelChunkWithLightPacket
//Fields: X(int), Z(int), ChunkData(ChunkAccess), LightData(ClientboundLightUpdatePacketData), PreEncoded (pre-serialized payload)
//ChunkData is relaxed to ChunkAccess to accept the ProtoChunk produced by the generation chain; serialization goes through LevelChunkSerializer
//Packet encoding has moved to a background write thread; reading ChunkAccess during encoding would race with the main thread modifying chunks
//Bulk sending goes through CreatePrepared, serializing on the calling thread first so encoding only writes PreEncoded
public sealed record ClientboundLevelChunkWithLightPacket(int X, int Z, ChunkAccess? ChunkData,
    ClientboundLightUpdatePacketData? LightData, byte[]? PreEncoded = null) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundLevelChunkWithLightPacket> StreamCodec { get; } = new LevelChunkWithLightCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundLevelChunkWithLight;

    public void Handle(ClientGamePacketListener handler) => handler.HandleLevelChunkWithLight(this);

    //CreatePrepared freezes the chunk and light into bytes on the calling thread; encoding afterwards never touches ChunkAccess
    //Maps to vanilla ClientboundLevelChunkPacketData copying section data into a buffer at construction time
    //lightFactory defers fetching light until the chunk is written, so the caller can shrink the lock window to just the light read
    //blockEntities is fetched per chunk by the caller; empty means the chunk has no block entities
    public static ClientboundLevelChunkWithLightPacket CreatePrepared(int x, int z, ChunkAccess chunk,
        Func<ClientboundLightUpdatePacketData> lightFactory, IReadOnlyList<CompoundTag>? blockEntities = null)
    {
        GameBootstrap.Bootstrap();
        var body = new FriendlyByteBuf();
        var factory = PalettedContainerFactory.Default;
        LevelChunkSerializer.Write(body, chunk, factory.CreateForBlockStates, factory.CreateForBiomes, blockEntities);
        lightFactory().Write(body);
        return new ClientboundLevelChunkWithLightPacket(x, z, null, null, body.ToArray());
    }

    private sealed class LevelChunkWithLightCodec : StreamCodec<FriendlyByteBuf, ClientboundLevelChunkWithLightPacket>
    {
        public ClientboundLevelChunkWithLightPacket Decode(FriendlyByteBuf buf)
        {
            GameBootstrap.Bootstrap();
            //S4 vanilla writeInt is 4 bytes, not VarInt
            var x = buf.ReadInt();
            var z = buf.ReadInt();
            var pos = new ChunkPos(x, z);
            var factory = PalettedContainerFactory.Default;
            var chunk = LevelChunkSerializer.Read(
                buf, pos, DefaultMinSectionY, DefaultSectionsCount,
                factory.CreateForBlockStates, factory.CreateForBiomes);
            var light = ClientboundLightUpdatePacketData.Read(buf);
            return new ClientboundLevelChunkWithLightPacket(x, z, chunk, light);
        }

        public void Encode(FriendlyByteBuf buf, ClientboundLevelChunkWithLightPacket value)
        {
            //S4 vanilla writeInt is 4 bytes, not VarInt
            buf.WriteInt(value.X);
            buf.WriteInt(value.Z);
            if (value.PreEncoded is not null)
            {
                buf.WriteBytes(value.PreEncoded);
                return;
            }
            GameBootstrap.Bootstrap();
            if (value.ChunkData is null)
                return;
            var factory = PalettedContainerFactory.Default;
            LevelChunkSerializer.Write(buf, value.ChunkData, factory.CreateForBlockStates, factory.CreateForBiomes);
            var light = value.LightData ?? ClientboundLightUpdatePacketData.Empty;
            light.Write(buf);
        }

        //DefaultMinSectionY/DefaultSectionsCount overworld defaults, maps to vanilla -64..320
        //Once real dimension config is wired up these derive from DimensionType; here they are simplified to constants
        private const int DefaultMinSectionY = -4;
        private const int DefaultSectionsCount = 24;
    }
}
