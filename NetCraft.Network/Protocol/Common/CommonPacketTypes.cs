using NetCraft.Registry;

namespace NetCraft.Network.Protocol.Common;

//CommonPacketTypes common packet type registration, maps to vanilla net.minecraft.network.protocol.common.CommonPacketTypes
//Ids reuse the numbering of the Play protocol space, for GameProtocols to bridge into Play with AddPacketCommon
//Other protocols have a different common packet id space (e.g. configuration starts at 0) and build their own set in their PacketTypes
public static class CommonPacketTypes
{
    //Clientbound (Play, Clientbound)

    //ClientboundCustomPayload custom payload packet
    public static readonly PacketType<ClientCommonPacketListener> ClientboundCustomPayload =
        CreateClientbound<ClientCommonPacketListener>(24, "custom_payload");

    //ClientboundDisconnect connection disconnect packet
    public static readonly PacketType<ClientCommonPacketListener> ClientboundDisconnect =
        CreateClientbound<ClientCommonPacketListener>(32, "disconnect");

    //ClientboundKeepAlive keep-alive packet
    public static readonly PacketType<ClientCommonPacketListener> ClientboundKeepAlive =
        CreateClientbound<ClientCommonPacketListener>(44, "keep_alive");

    //ClientboundPing latency probe packet
    public static readonly PacketType<ClientCommonPacketListener> ClientboundPing =
        CreateClientbound<ClientCommonPacketListener>(61, "ping");

    //ClientboundResourcePackPop resource pack pop
    public static readonly PacketType<ClientCommonPacketListener> ClientboundResourcePackPop =
        CreateClientbound<ClientCommonPacketListener>(80, "resource_pack_pop");

    //ClientboundResourcePackPush resource pack push
    public static readonly PacketType<ClientCommonPacketListener> ClientboundResourcePackPush =
        CreateClientbound<ClientCommonPacketListener>(81, "resource_pack_push");

    //ClientboundStoreCookie store cookie
    public static readonly PacketType<ClientCommonPacketListener> ClientboundStoreCookie =
        CreateClientbound<ClientCommonPacketListener>(120, "store_cookie");

    //ClientboundTransfer transfer the connection to another server
    public static readonly PacketType<ClientCommonPacketListener> ClientboundTransfer =
        CreateClientbound<ClientCommonPacketListener>(129, "transfer");

    //ClientboundUpdateTags tag synchronization
    public static readonly PacketType<ClientCommonPacketListener> ClientboundUpdateTags =
        CreateClientbound<ClientCommonPacketListener>(134, "update_tags");

    //ClientboundCustomReportDetails custom report details
    public static readonly PacketType<ClientCommonPacketListener> ClientboundCustomReportDetails =
        CreateClientbound<ClientCommonPacketListener>(136, "custom_report_details");

    //ClientboundServerLinks server links
    public static readonly PacketType<ClientCommonPacketListener> ClientboundServerLinks =
        CreateClientbound<ClientCommonPacketListener>(137, "server_links");

    //ClientboundClearDialog close dialog
    public static readonly PacketType<ClientCommonPacketListener> ClientboundClearDialog =
        CreateClientbound<ClientCommonPacketListener>(139, "clear_dialog");

    //ClientboundShowDialog show dialog
    public static readonly PacketType<ClientCommonPacketListener> ClientboundShowDialog =
        CreateClientbound<ClientCommonPacketListener>(140, "show_dialog");

    //Serverbound (Play, Serverbound)

    //ServerboundClientInformation client information
    public static readonly PacketType<ServerCommonPacketListener> ServerboundClientInformation =
        CreateServerbound<ServerCommonPacketListener>(14, "client_information");

    //ServerboundCustomPayload custom payload
    public static readonly PacketType<ServerCommonPacketListener> ServerboundCustomPayload =
        CreateServerbound<ServerCommonPacketListener>(22, "custom_payload");

    //ServerboundKeepAlive keep-alive reply
    public static readonly PacketType<ServerCommonPacketListener> ServerboundKeepAlive =
        CreateServerbound<ServerCommonPacketListener>(28, "keep_alive");

    //ServerboundPong latency probe reply
    public static readonly PacketType<ServerCommonPacketListener> ServerboundPong =
        CreateServerbound<ServerCommonPacketListener>(45, "pong");

    //ServerboundResourcePack resource pack status acknowledgement
    public static readonly PacketType<ServerCommonPacketListener> ServerboundResourcePack =
        CreateServerbound<ServerCommonPacketListener>(49, "resource_pack");

    //ServerboundCustomClickAction custom click action
    public static readonly PacketType<ServerCommonPacketListener> ServerboundCustomClickAction =
        CreateServerbound<ServerCommonPacketListener>(68, "custom_click_action");

    //CreateClientbound registers a clientbound packet type with its id as the network ID
    private static PacketType<THandler> CreateClientbound<THandler>(int id, string identifier)
        where THandler : class
        => PacketTypeRegistry.Register<THandler>(id, ConnectionProtocol.Play, FlowDirection.Clientbound)
            .WithIdentifier(Identifier.WithDefaultNamespace(identifier));

    //CreateServerbound registers a serverbound packet type
    private static PacketType<THandler> CreateServerbound<THandler>(int id, string identifier)
        where THandler : class
        => PacketTypeRegistry.Register<THandler>(id, ConnectionProtocol.Play, FlowDirection.Serverbound)
            .WithIdentifier(Identifier.WithDefaultNamespace(identifier));
}
