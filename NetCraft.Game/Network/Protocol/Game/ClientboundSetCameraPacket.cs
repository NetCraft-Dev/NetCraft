namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetCameraPacket 设置视角包对应原版 ClientboundSetCameraPacket
//字段 CameraId(int) 目标实体网络 id 客户端据此把视角切到该实体
public sealed record ClientboundSetCameraPacket(int CameraId) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetCameraPacket> StreamCodec { get; } = new SetCameraCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetCamera;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetCamera(this);

    private sealed class SetCameraCodec : StreamCodec<FriendlyByteBuf, ClientboundSetCameraPacket>
    {
        //原版 write 只写一个 VarInt 实体 id
        public ClientboundSetCameraPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ClientboundSetCameraPacket value)
            => buf.WriteVarInt(value.CameraId);
    }
}
