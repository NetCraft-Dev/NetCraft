namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundRespawnPacket 重生包对应原版 ClientboundRespawnPacket
//字段 commonPlayerSpawnInfo CommonPlayerSpawnInfo 业务类型占位 dataToKeep byte
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
            => throw new NotImplementedException("CommonPlayerSpawnInfo 业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundRespawnPacket value)
            => throw new NotImplementedException("CommonPlayerSpawnInfo 业务类型待实现");
    }
}
