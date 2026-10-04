
namespace NetCraft.Network.Protocol.Common;

//ServerboundResourcePackPacket 资源包应答包对应原版 net.minecraft.network.protocol.common.ServerboundResourcePackPacket
//含 UUID id + Action 枚举客户端告知服务端资源包处理状态
public sealed record ServerboundResourcePackPacket(Guid Id, ResourcePackAction Action) : Packet<ServerCommonPacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundResourcePackPacket> StreamCodec { get; } = new ResourcePackCodec();

    public PacketType<ServerCommonPacketListener> Type => CommonPacketTypes.ServerboundResourcePack;

    public void Handle(ServerCommonPacketListener handler) => handler.HandleResourcePack(this);

    private sealed class ResourcePackCodec : StreamCodec<FriendlyByteBuf, ServerboundResourcePackPacket>
    {
        public ServerboundResourcePackPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadUuid(), (ResourcePackAction)buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ServerboundResourcePackPacket value)
        {
            buf.WriteUuid(value.Id);
            buf.WriteVarInt((int)value.Action);
        }
    }
}

//ResourcePackAction 资源包处理状态枚举对应原版 ServerboundResourcePackPacket.Action
//SuccessfullLoaded/Declined/FailedDownload/Accepted/Downloaded/InvalidUrl/FailedReload/Discarded
//Accepted 和 Downloaded 是中间状态其他都是终态
public enum ResourcePackAction
{
    SuccessfullyLoaded,
    Declined,
    FailedDownload,
    Accepted,
    Downloaded,
    InvalidUrl,
    FailedReload,
    Discarded
}

//ResourcePackActionExtensions 资源包状态扩展方法
public static class ResourcePackActionExtensions
{
    //IsTerminal 是否终态 ACCEPTED 和 DOWNLOADED 是中间状态其他都是终态
    public static bool IsTerminal(this ResourcePackAction action)
        => action != ResourcePackAction.Accepted && action != ResourcePackAction.Downloaded;
}
