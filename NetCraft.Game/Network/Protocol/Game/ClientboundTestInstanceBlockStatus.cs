namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundTestInstanceBlockStatus 测试方块状态包对应原版 ClientboundTestInstanceBlockStatus
//字段 Status(Component) Size(Optional<Vec3i>)
public sealed record ClientboundTestInstanceBlockStatus(object Status, object Size) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundTestInstanceBlockStatus> StreamCodec { get; } = new TestInstanceBlockStatusCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundTestInstanceBlockStatus;

    public void Handle(ClientGamePacketListener handler) => handler.HandleTestInstanceBlockStatus(this);

    private sealed class TestInstanceBlockStatusCodec : StreamCodec<FriendlyByteBuf, ClientboundTestInstanceBlockStatus>
    {
        public ClientboundTestInstanceBlockStatus Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundTestInstanceBlockStatus value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
