using NetCraft.Network;
using NetCraft.Registry;
using NetCraft.Storage;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundLoginPacket login packet, maps to vanilla ClientboundLoginPacket
//Fields: PlayerId(VarInt), Hardcore(boolean), Levels(ResourceKey<Level>[]), MaxPlayers(VarInt), ChunkRadius(VarInt), SimulationDistance(VarInt)
//ReducedDebugInfo(boolean), ShowDeathScreen(boolean), DoLimitedCrafting(boolean), CommonPlayerSpawnInfo, EnforcesSecureChat(boolean)
public sealed record ClientboundLoginPacket(
    int PlayerId,
    bool Hardcore,
    IReadOnlyList<ResourceKey<Level>> Levels,
    int MaxPlayers,
    int ChunkRadius,
    int SimulationDistance,
    bool ReducedDebugInfo,
    bool ShowDeathScreen,
    bool DoLimitedCrafting,
    CommonPlayerSpawnInfo CommonPlayerSpawnInfo,
    bool EnforcesSecureChat) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundLoginPacket> StreamCodec { get; } = new LoginCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundLogin;

    public void Handle(ClientGamePacketListener handler) => handler.HandleLogin(this);

    private sealed class LoginCodec : StreamCodec<FriendlyByteBuf, ClientboundLoginPacket>
    {
        public ClientboundLoginPacket Decode(FriendlyByteBuf buf)
        {
            //S4 vanilla playerId is writeInt 4 bytes, not VarInt
            var playerId = buf.ReadInt();
            var hardcore = buf.ReadBoolean();
            var levelCount = buf.ReadVarInt();
            var levels = new ResourceKey<Level>[levelCount];
            for (var i = 0; i < levelCount; i++)
                levels[i] = ResourceKey<Level>.Create(Registries.DIMENSION, buf.ReadIdentifier());
            var maxPlayers = buf.ReadVarInt();
            var chunkRadius = buf.ReadVarInt();
            var simulationDistance = buf.ReadVarInt();
            var reducedDebugInfo = buf.ReadBoolean();
            var showDeathScreen = buf.ReadBoolean();
            var doLimitedCrafting = buf.ReadBoolean();
            var spawnInfo = CommonPlayerSpawnInfo.StaticCodec.Decode(buf);
            //S4 26.2 has two booleans onlineMode + enforcesSecureChat; onlineMode is discarded after decoding
            buf.ReadBoolean();
            var enforcesSecureChat = buf.ReadBoolean();
            return new ClientboundLoginPacket(playerId, hardcore, levels, maxPlayers, chunkRadius,
                simulationDistance, reducedDebugInfo, showDeathScreen, doLimitedCrafting,
                spawnInfo, enforcesSecureChat);
        }

        public void Encode(FriendlyByteBuf buf, ClientboundLoginPacket value)
        {
            //S4 vanilla writeInt(playerId) 4 bytes
            buf.WriteInt(value.PlayerId);
            buf.WriteBoolean(value.Hardcore);
            buf.WriteVarInt(value.Levels.Count);
            foreach (var level in value.Levels)
                buf.WriteIdentifier(level.Identifier);
            buf.WriteVarInt(value.MaxPlayers);
            buf.WriteVarInt(value.ChunkRadius);
            buf.WriteVarInt(value.SimulationDistance);
            buf.WriteBoolean(value.ReducedDebugInfo);
            buf.WriteBoolean(value.ShowDeathScreen);
            buf.WriteBoolean(value.DoLimitedCrafting);
            CommonPlayerSpawnInfo.StaticCodec.Encode(buf, value.CommonPlayerSpawnInfo);
            //S4 26.2 onlineMode boolean, false for an offline server
            buf.WriteBoolean(false);
            buf.WriteBoolean(value.EnforcesSecureChat);
        }
    }
}
