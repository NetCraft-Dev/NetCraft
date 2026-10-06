namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundTestInstanceBlockStatus test instance block status packet, maps to vanilla ClientboundTestInstanceBlockStatus
//Fields: Status(Component), Size(Optional<Vec3i>)
public sealed record ClientboundTestInstanceBlockStatus(object Status, object Size) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundTestInstanceBlockStatus> StreamCodec { get; } = new TestInstanceBlockStatusCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundTestInstanceBlockStatus;

    public void Handle(ClientGamePacketListener handler) => handler.HandleTestInstanceBlockStatus(this);

    private sealed class TestInstanceBlockStatusCodec : StreamCodec<FriendlyByteBuf, ClientboundTestInstanceBlockStatus>
    {
        public ClientboundTestInstanceBlockStatus Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundTestInstanceBlockStatus value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
