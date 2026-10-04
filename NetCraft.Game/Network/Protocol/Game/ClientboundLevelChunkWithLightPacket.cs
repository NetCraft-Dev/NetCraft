using NetCraft.Game.Bootstrap;
using NetCraft.Game.World.Level.Chunk;
using NetCraft.Nbt;
using NetCraft.Network;
using NetCraft.Primitives;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;
using NetCraft.Storage.Paletted;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundLevelChunkWithLightPacket 带光照区块包对应原版 ClientboundLevelChunkWithLightPacket
//字段 X(int) Z(int) ChunkData(ChunkAccess) LightData(ClientboundLightUpdatePacketData) PreEncoded(预序列化载荷)
//ChunkData 放宽为 ChunkAccess 兼容生成链产出的 ProtoChunk 序列化走 LevelChunkSerializer
//发包编码已挪到后台写线程 编码期再读 ChunkAccess 会与主线程改块并发
//批量下发走 CreatePrepared 在调用线程先序列化好 编码时只写 PreEncoded
public sealed record ClientboundLevelChunkWithLightPacket(int X, int Z, ChunkAccess? ChunkData,
    ClientboundLightUpdatePacketData? LightData, byte[]? PreEncoded = null) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundLevelChunkWithLightPacket> StreamCodec { get; } = new LevelChunkWithLightCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundLevelChunkWithLight;

    public void Handle(ClientGamePacketListener handler) => handler.HandleLevelChunkWithLight(this);

    //CreatePrepared 在调用线程把区块与光照固化成字节 之后编码不再触达 ChunkAccess
    //对应原版 ClientboundLevelChunkPacketData 构造时就把区段数据抄进 buffer
    //lightFactory 延迟到区块写完才取光照 调用方据此把持锁窗口缩到只读光照那一小段
    //blockEntities 由调用方按区块取 为空表示该区块没有方块实体
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
            //S4 原版 writeInt 4 字节非 VarInt
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
            //S4 原版 writeInt 4 字节非 VarInt
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

        //DefaultMinSectionY/DefaultSectionsCount 主世界默认参数对应原版 -64..320
        //真实接入维度配置后由 DimensionType 派生此处简化为常量
        private const int DefaultMinSectionY = -4;
        private const int DefaultSectionsCount = 24;
    }
}
