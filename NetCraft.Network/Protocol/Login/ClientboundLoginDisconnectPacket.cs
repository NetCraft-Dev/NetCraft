namespace NetCraft.Network.Protocol.Login;

//ClientboundLoginDisconnectPacket 服务端登录断开包对应原版 net.minecraft.network.protocol.login.ClientboundLoginDisconnectPacket
//含 reason 断开原因原版用 Component 简化版用 string
//IsTerminal true 表示断开后连接关闭
public sealed record ClientboundLoginDisconnectPacket(string Reason) : Packet<ClientLoginPacketListener>
{
    //MaxReasonLength reason 字符串最大长度对齐 FriendlyByteBuf.MAX_COMPONENT_STRING_LENGTH
    public const int MaxReasonLength = 262144;

    //StreamCodec 包编解码器
    public static StreamCodec<FriendlyByteBuf, ClientboundLoginDisconnectPacket> StreamCodec { get; } = new DisconnectCodec();

    public PacketType<ClientLoginPacketListener> Type => LoginPacketTypes.ClientboundLoginDisconnect;

    //IsTerminal 断开包后连接关闭
    public bool IsTerminal => true;

    public void Handle(ClientLoginPacketListener handler) => handler.HandleDisconnect(this);

    //DisconnectCodec 编解码器读写 reason 字符串
    private sealed class DisconnectCodec : StreamCodec<FriendlyByteBuf, ClientboundLoginDisconnectPacket>
    {
        public ClientboundLoginDisconnectPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadString(MaxReasonLength));

        public void Encode(FriendlyByteBuf buf, ClientboundLoginDisconnectPacket value)
            => buf.WriteString(value.Reason, MaxReasonLength);
    }
}
