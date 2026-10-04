namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundPlayerRotationPacket 玩家旋转包对应原版 ClientboundPlayerRotationPacket
//字段 YRot(float) RelativeY(boolean) XRot(float) RelativeX(boolean)
public sealed record ClientboundPlayerRotationPacket(float YRot, bool RelativeY, float XRot, bool RelativeX) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundPlayerRotationPacket> StreamCodec { get; } = new PlayerRotationCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundPlayerRotation;

    public void Handle(ClientGamePacketListener handler) => handler.HandleRotatePlayer(this);

    private sealed class PlayerRotationCodec : StreamCodec<FriendlyByteBuf, ClientboundPlayerRotationPacket>
    {
        //原版顺序 float 偏航 布尔 偏航是否相对 float 俯仰 布尔 俯仰是否相对
        public ClientboundPlayerRotationPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadFloat(), buf.ReadBoolean(), buf.ReadFloat(), buf.ReadBoolean());

        public void Encode(FriendlyByteBuf buf, ClientboundPlayerRotationPacket value)
        {
            buf.WriteFloat(value.YRot);
            buf.WriteBoolean(value.RelativeY);
            buf.WriteFloat(value.XRot);
            buf.WriteBoolean(value.RelativeX);
        }
    }
}
