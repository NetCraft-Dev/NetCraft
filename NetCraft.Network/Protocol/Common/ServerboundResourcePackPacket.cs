
namespace NetCraft.Network.Protocol.Common;

//ServerboundResourcePackPacket resource pack response packet, maps to vanilla net.minecraft.network.protocol.common.ServerboundResourcePackPacket
//Contains UUID id + the Action enum; the client tells the server the resource pack processing status
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

//ResourcePackAction resource pack processing status enum, maps to vanilla ServerboundResourcePackPacket.Action
//SuccessfullLoaded/Declined/FailedDownload/Accepted/Downloaded/InvalidUrl/FailedReload/Discarded
//Accepted and Downloaded are intermediate states, the rest are terminal
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

//ResourcePackActionExtensions extension methods for resource pack status
public static class ResourcePackActionExtensions
{
    //IsTerminal whether it is terminal; ACCEPTED and DOWNLOADED are intermediate states, the rest are terminal
    public static bool IsTerminal(this ResourcePackAction action)
        => action != ResourcePackAction.Accepted && action != ResourcePackAction.Downloaded;
}
