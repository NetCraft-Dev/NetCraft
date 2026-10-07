namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundTestInstanceBlockActionPacket test instance block action packet, maps to vanilla ServerboundTestInstanceBlockActionPacket
//Fields: Pos(BlockPos), Action(Action), Data(TestInstanceBlockEntity.Data)
public sealed record ServerboundTestInstanceBlockActionPacket(object Pos, object Action, object Data) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundTestInstanceBlockActionPacket> StreamCodec { get; } = new TestInstanceBlockActionCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundTestInstanceBlockAction;

    public void Handle(ServerGamePacketListener handler) => handler.HandleTestInstanceBlockAction(this);

    private sealed class TestInstanceBlockActionCodec : StreamCodec<FriendlyByteBuf, ServerboundTestInstanceBlockActionPacket>
    {
        public ServerboundTestInstanceBlockActionPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ServerboundTestInstanceBlockActionPacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
