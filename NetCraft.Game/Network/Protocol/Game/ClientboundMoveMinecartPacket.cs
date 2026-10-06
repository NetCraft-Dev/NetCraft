namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundMoveMinecartPacket minecart movement packet, maps to vanilla ClientboundMoveMinecartPacket
//Fields: EntityId(int), LerpSteps(List<NewMinecartBehavior.MinecartStep>)
public sealed record ClientboundMoveMinecartPacket(int EntityId, object LerpSteps) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundMoveMinecartPacket> StreamCodec { get; } = new MoveMinecartCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundMoveMinecartAlongTrack;

    public void Handle(ClientGamePacketListener handler) => handler.HandleMinecartAlongTrack(this);

    private sealed class MoveMinecartCodec : StreamCodec<FriendlyByteBuf, ClientboundMoveMinecartPacket>
    {
        public ClientboundMoveMinecartPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundMoveMinecartPacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
