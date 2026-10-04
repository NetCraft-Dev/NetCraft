namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundDebugEventPacket 调试事件包对应原版 ClientboundDebugEventPacket
//字段 Event(DebugSubscription.Event<?>)
public sealed record ClientboundDebugEventPacket(object Event) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundDebugEventPacket> StreamCodec { get; } = new DebugEventCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundDebugEvent;

    public void Handle(ClientGamePacketListener handler) => handler.HandleDebugEvent(this);

    private sealed class DebugEventCodec : StreamCodec<FriendlyByteBuf, ClientboundDebugEventPacket>
    {
        public ClientboundDebugEventPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundDebugEventPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
