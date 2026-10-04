
namespace NetCraft.Network.Protocol.Common;

//ClientboundResourcePackPopPacket 资源包弹出包对应原版 net.minecraft.network.protocol.common.ClientboundResourcePackPopPacket
//含可空 UUID id 表示弹出指定资源包或全部
public sealed record ClientboundResourcePackPopPacket(Guid? Id) : Packet<ClientCommonPacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundResourcePackPopPacket> StreamCodec { get; } = new ResourcePackPopCodec();

    public PacketType<ClientCommonPacketListener> Type => CommonPacketTypes.ClientboundResourcePackPop;

    public void Handle(ClientCommonPacketListener handler) => handler.HandleResourcePackPop(this);

    private sealed class ResourcePackPopCodec : StreamCodec<FriendlyByteBuf, ClientboundResourcePackPopPacket>
    {
        //Guid 是 struct 不能用 ReadNullable/WriteNullable 手动 boolean 标志位处理
        public ClientboundResourcePackPopPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadBoolean() ? buf.ReadUuid() : (Guid?)null);

        public void Encode(FriendlyByteBuf buf, ClientboundResourcePackPopPacket value)
        {
            if (value.Id.HasValue)
            {
                buf.WriteBoolean(true);
                buf.WriteUuid(value.Id.Value);
            }
            else
            {
                buf.WriteBoolean(false);
            }
        }
    }
}
