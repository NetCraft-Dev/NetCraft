using NetCraft.Registry;

namespace NetCraft.Network.Protocol.Common;

//ClientboundCustomPayloadPacket 自定义载荷包对应原版 net.minecraft.network.protocol.common.ClientboundCustomPayloadPacket
//原版依赖 CustomPayload 辅助类型按 id 分发不同 payload 类型
//简化版用 Identifier id + byte[] payload 透传原始字节
//CONFIG_STREAM_CODEC 和 STREAM_CODEC 同义共享简化编解码
public sealed record ClientboundCustomPayloadPacket(Identifier Id, byte[] Payload) : Packet<ClientCommonPacketListener>
{
    //MaxPayloadLength payload 最大长度 1048576 对齐原版 MAX_PAYLOAD_SIZE
    public const int MaxPayloadLength = 1048576;

    //StreamCodec 包编解码器通用版本
    public static StreamCodec<FriendlyByteBuf, ClientboundCustomPayloadPacket> StreamCodec { get; } = new CustomPayloadCodec();

    //ConfigStreamCodec 配置阶段版本对齐原版 CONFIG_STREAM_CODEC
    public static StreamCodec<FriendlyByteBuf, ClientboundCustomPayloadPacket> ConfigStreamCodec => StreamCodec;

    public PacketType<ClientCommonPacketListener> Type => CommonPacketTypes.ClientboundCustomPayload;

    public void Handle(ClientCommonPacketListener handler) => handler.HandleCustomPayload(this);

    private sealed class CustomPayloadCodec : StreamCodec<FriendlyByteBuf, ClientboundCustomPayloadPacket>
    {
        public ClientboundCustomPayloadPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadIdentifier(), buf.ReadByteArray(MaxPayloadLength));

        public void Encode(FriendlyByteBuf buf, ClientboundCustomPayloadPacket value)
        {
            buf.WriteIdentifier(value.Id);
            buf.WriteByteArray(value.Payload, MaxPayloadLength);
        }
    }
}
