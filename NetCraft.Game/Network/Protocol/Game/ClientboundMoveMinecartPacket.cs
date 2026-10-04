namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundMoveMinecartPacket 矿车移动包对应原版 ClientboundMoveMinecartPacket
//字段 EntityId(int) LerpSteps(List<NewMinecartBehavior.MinecartStep>)
public sealed record ClientboundMoveMinecartPacket(int EntityId, object LerpSteps) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundMoveMinecartPacket> StreamCodec { get; } = new MoveMinecartCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundMoveMinecartAlongTrack;

    public void Handle(ClientGamePacketListener handler) => handler.HandleMinecartAlongTrack(this);

    private sealed class MoveMinecartCodec : StreamCodec<FriendlyByteBuf, ClientboundMoveMinecartPacket>
    {
        public ClientboundMoveMinecartPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundMoveMinecartPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
