
namespace NetCraft.Network.Protocol.Common;

//ClientboundResourcePackPopPacket resource pack pop packet, maps to vanilla net.minecraft.network.protocol.common.ClientboundResourcePackPopPacket
//Contains a nullable UUID id, meaning pop the given resource pack or all of them
public sealed record ClientboundResourcePackPopPacket(Guid? Id) : Packet<ClientCommonPacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundResourcePackPopPacket> StreamCodec { get; } = new ResourcePackPopCodec();

    public PacketType<ClientCommonPacketListener> Type => CommonPacketTypes.ClientboundResourcePackPop;

    public void Handle(ClientCommonPacketListener handler) => handler.HandleResourcePackPop(this);

    private sealed class ResourcePackPopCodec : StreamCodec<FriendlyByteBuf, ClientboundResourcePackPopPacket>
    {
        //Guid is a struct and cannot use ReadNullable/WriteNullable, so a boolean flag is handled manually
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
