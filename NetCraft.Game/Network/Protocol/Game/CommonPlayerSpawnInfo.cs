using NetCraft.Network.Protocol.Configuration;
using NetCraft.Game.World.Level;
using NetCraft.Network;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;

namespace NetCraft.Game.Network.Protocol.Game;

//CommonPlayerSpawnInfo common player spawn info, maps to vanilla CommonPlayerSpawnInfo
//Shared by ClientboundLoginPacket/RespawnPacket; carries dimension/seed/game mode/death location/portal cooldown
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
    //DimensionTypeRegistry dimension type registry id; the login packet's dimensionType is encoded by its synchronized int id
    private const string DimensionTypeRegistry = "minecraft:dimension_type";

    //StaticCodec shared codec reused by Login/Respawn
    public static StreamCodec<FriendlyByteBuf, CommonPlayerSpawnInfo> StaticCodec { get; } = new CommonPlayerSpawnInfoCodec();

    private sealed class CommonPlayerSpawnInfoCodec : StreamCodec<FriendlyByteBuf, CommonPlayerSpawnInfo>
    {
        public CommonPlayerSpawnInfo Decode(FriendlyByteBuf buf)
        {
            //Vanilla holderRegistry is encoded as a VarInt registry id; writing an Identifier would make the client read the length prefix as the id
            var dimensionType = SynchronizedRegistryData.GetEntry(DimensionTypeRegistry, buf.ReadVarInt())
                ?? Identifier.WithDefaultNamespace("overworld");
            var dimension = ResourceKey<Level>.Create(Registries.DIMENSION, buf.ReadIdentifier());
            var seed = buf.ReadLong();
            //S4 vanilla GameType.byId(readByte) single byte
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
            //S4 26.2 adds a seaLevel varint, discarded after decoding
            buf.ReadVarInt();
            return new CommonPlayerSpawnInfo(dimensionType, dimension, seed, gameType,
                previousGameType, isDebug, isFlat, hasDeath, deathDim, deathPos, portalCooldown);
        }

        public void Encode(FriendlyByteBuf buf, CommonPlayerSpawnInfo value)
        {
            //Vanilla holderRegistry is encoded as the synchronized int id; an Identifier must not be written
            buf.WriteVarInt(SynchronizedRegistryData.GetEntryId(DimensionTypeRegistry, value.DimensionType));
            buf.WriteIdentifier(value.Dimension.Identifier);
            buf.WriteLong(value.Seed);
            //S4 vanilla writeByte(gameType.getId()) single byte
            buf.WriteByte((byte)value.GameType.Id);
            //previousGameType vanilla writeByte(-1 or id) single byte; VarInt(-1) is 5 bytes and must not be used
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
            //S4 26.2 adds a seaLevel varint; the vanilla sea level is 63
            buf.WriteVarInt(63);
        }
    }
}
