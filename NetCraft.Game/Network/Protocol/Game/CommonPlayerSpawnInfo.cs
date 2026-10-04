using NetCraft.Network.Protocol.Configuration;
using NetCraft.Game.World.Level;
using NetCraft.Network;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;

namespace NetCraft.Game.Network.Protocol.Game;

//CommonPlayerSpawnInfo 玩家出生公共信息对应原版 CommonPlayerSpawnInfo
//ClientboundLoginPacket/RespawnPacket 共用 含维度/种子/游戏模式/死亡点/传送门冷却
public sealed record CommonPlayerSpawnInfo(
    Identifier DimensionType,
    ResourceKey<Level> Dimension,
    long Seed,
    GameType GameType,
    GameType? PreviousGameType,
    bool IsDebug,
    bool IsFlat,
    bool HasLastDeathLocation,
    ResourceKey<Level>? LastDeathDimension,
    BlockPos? LastDeathPosition,
    int PortalCooldown)
{
    //DimensionTypeRegistry 维度类型注册表 id 登录包的 dimensionType 按其同步顺序 int id 编码
    private const string DimensionTypeRegistry = "minecraft:dimension_type";

    //StaticCodec 公共编解码器供 Login/Respawn 复用
    public static StreamCodec<FriendlyByteBuf, CommonPlayerSpawnInfo> StaticCodec { get; } = new CommonPlayerSpawnInfoCodec();

    private sealed class CommonPlayerSpawnInfoCodec : StreamCodec<FriendlyByteBuf, CommonPlayerSpawnInfo>
    {
        public CommonPlayerSpawnInfo Decode(FriendlyByteBuf buf)
        {
            //原版 holderRegistry 编码为 VarInt 注册表 id 写 Identifier 会让客户端把长度前缀当 id
            var dimensionType = SynchronizedRegistryData.GetEntry(DimensionTypeRegistry, buf.ReadVarInt())
                ?? Identifier.WithDefaultNamespace("overworld");
            var dimension = ResourceKey<Level>.Create(Registries.DIMENSION, buf.ReadIdentifier());
            var seed = buf.ReadLong();
            //S4 原版 GameType.byId(readByte) 单字节
            var gameType = GameType.ById(buf.ReadByte()) ?? GameType.Survival;
            var prevRaw = buf.ReadByte();
            var previousGameType = prevRaw == -1 ? null : GameType.ById(prevRaw);
            var isDebug = buf.ReadBoolean();
            var isFlat = buf.ReadBoolean();
            var hasDeath = buf.ReadBoolean();
            ResourceKey<Level>? deathDim = null;
            BlockPos? deathPos = null;
            if (hasDeath)
            {
                deathDim = ResourceKey<Level>.Create(Registries.DIMENSION, buf.ReadIdentifier());
                var packed = buf.ReadLong();
                deathPos = new BlockPos(BlockPos.GetX(packed), BlockPos.GetY(packed), BlockPos.GetZ(packed));
            }
            var portalCooldown = buf.ReadVarInt();
            //S4 26.2 新增 seaLevel varint 解码后丢弃
            buf.ReadVarInt();
            return new CommonPlayerSpawnInfo(dimensionType, dimension, seed, gameType,
                previousGameType, isDebug, isFlat, hasDeath, deathDim, deathPos, portalCooldown);
        }

        public void Encode(FriendlyByteBuf buf, CommonPlayerSpawnInfo value)
        {
            //原版 holderRegistry 编码为同步顺序 int id 不能写 Identifier
            buf.WriteVarInt(SynchronizedRegistryData.GetEntryId(DimensionTypeRegistry, value.DimensionType));
            buf.WriteIdentifier(value.Dimension.Identifier);
            buf.WriteLong(value.Seed);
            //S4 原版 writeByte(gameType.getId()) 单字节
            buf.WriteByte((byte)value.GameType.Id);
            //previousGameType 原版 writeByte(-1 或 id) 单字节不能用 VarInt(-1) 5 字节
            buf.WriteByte((byte)(value.PreviousGameType?.Id ?? -1));
            buf.WriteBoolean(value.IsDebug);
            buf.WriteBoolean(value.IsFlat);
            buf.WriteBoolean(value.HasLastDeathLocation);
            if (value.HasLastDeathLocation)
            {
                buf.WriteIdentifier(value.LastDeathDimension!.Identifier);
                buf.WriteLong(value.LastDeathPosition!.Value.AsLong());
            }
            buf.WriteVarInt(value.PortalCooldown);
            //S4 26.2 新增 seaLevel varint 原版海平面 63
            buf.WriteVarInt(63);
        }
    }
}
