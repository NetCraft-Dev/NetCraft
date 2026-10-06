namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundRespawnPacket respawn packet, maps to vanilla ClientboundRespawnPacket
//Fields: commonPlayerSpawnInfo CommonPlayerSpawnInfo business type placeholder, dataToKeep byte
public sealed record ClientboundRespawnPacket(object CommonPlayerSpawnInfo, byte DataToKeep) : Packet<ClientGamePacketListener>
{
    public const byte KeepAttributeModifiers = 1;
    public const byte KeepEntityData = 2;
    public const byte KeepAllData = 3;

    public static StreamCodec<FriendlyByteBuf, ClientboundRespawnPacket> StreamCodec { get; } = new RespawnCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundRespawn;

    public void Handle(ClientGamePacketListener handler) => handler.HandleRespawn(this);

    public bool ShouldKeep(byte mask) => (DataToKeep & mask) != 0;

    private sealed class RespawnCodec : StreamCodec<FriendlyByteBuf, ClientboundRespawnPacket>
    {
        public ClientboundRespawnPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("CommonPlayerSpawnInfo business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundRespawnPacket value)
            => throw new NotImplementedException("CommonPlayerSpawnInfo business type not yet implemented");
    }
}
