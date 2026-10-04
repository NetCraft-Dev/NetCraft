namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundBossEventPacket Boss 血条事件包对应原版 ClientboundBossEventPacket
//字段 id UUID operation Operation 业务类型占位
public sealed record ClientboundBossEventPacket(Guid Id, object Operation) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundBossEventPacket> StreamCodec { get; } = new BossEventCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundBossEvent;

    public void Handle(ClientGamePacketListener handler) => handler.HandleBossUpdate(this);

    private sealed class BossEventCodec : StreamCodec<FriendlyByteBuf, ClientboundBossEventPacket>
    {
        public ClientboundBossEventPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("BossEvent.Operation 业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundBossEventPacket value)
            => throw new NotImplementedException("BossEvent.Operation 业务类型待实现");
    }
}
