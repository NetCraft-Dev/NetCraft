namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundSetCommandMinecartPacket 数据包对应原版 ServerboundSetCommandMinecartPacket
//字段 Entity(int) Command(String) TrackOutput(boolean)
public sealed record ServerboundSetCommandMinecartPacket(int Entity, string Command, bool TrackOutput) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundSetCommandMinecartPacket> StreamCodec { get; } = new SetCommandMinecartCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundSetCommandMinecart;

    public void Handle(ServerGamePacketListener handler) => handler.HandleSetCommandMinecart(this);

    private sealed class SetCommandMinecartCodec : StreamCodec<FriendlyByteBuf, ServerboundSetCommandMinecartPacket>
    {
        public ServerboundSetCommandMinecartPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundSetCommandMinecartPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
