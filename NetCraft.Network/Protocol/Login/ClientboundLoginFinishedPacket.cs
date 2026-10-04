namespace NetCraft.Network.Protocol.Login;

//ClientboundLoginFinishedPacket 服务端登录完成包对应原版 net.minecraft.network.protocol.login.ClientboundLoginFinishedPacket
//含 GameProfile 和 sessionId UUID
//IsTerminal true 表示登录完成切换到 CONFIGURATION
public sealed record ClientboundLoginFinishedPacket(GameProfile GameProfile, Guid SessionId) : Packet<ClientLoginPacketListener>
{
    //StreamCodec 包编解码器
    public static StreamCodec<FriendlyByteBuf, ClientboundLoginFinishedPacket> StreamCodec { get; } = new FinishedCodec();

    public PacketType<ClientLoginPacketListener> Type => LoginPacketTypes.ClientboundLoginFinished;

    //IsTerminal 登录完成后切换到 CONFIGURATION
    public bool IsTerminal => true;

    public void Handle(ClientLoginPacketListener handler) => handler.HandleLoginFinished(this);

    //FinishedCodec 编解码器读写 GameProfile(uuid+name+properties) + sessionId UUID
    //S4 对齐原版 ByteBufCodecs.GAME_PROFILE = UUID + PLAYER_NAME + GAME_PROFILE_PROPERTIES
    private sealed class FinishedCodec : StreamCodec<FriendlyByteBuf, ClientboundLoginFinishedPacket>
    {
        public ClientboundLoginFinishedPacket Decode(FriendlyByteBuf buf)
        {
            var id = buf.ReadUuid();
            var name = buf.ReadString(16);
            var propertyCount = buf.ReadVarInt();
            for (var i = 0; i < propertyCount; i++)
            {
                buf.ReadString();
                buf.ReadString();
                if (buf.ReadBoolean()) buf.ReadByteArray();
            }
            var sessionId = buf.ReadUuid();
            return new ClientboundLoginFinishedPacket(new GameProfile(id, name), sessionId);
        }

        public void Encode(FriendlyByteBuf buf, ClientboundLoginFinishedPacket value)
        {
            buf.WriteUuid(value.GameProfile.Id);
            buf.WriteString(value.GameProfile.Name, 16);
            //properties 空列表 0 个 S4 不实现签名属性
            buf.WriteVarInt(0);
            buf.WriteUuid(value.SessionId);
        }
    }
}
