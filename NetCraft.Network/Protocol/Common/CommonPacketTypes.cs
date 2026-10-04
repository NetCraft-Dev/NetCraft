using NetCraft.Registry;

namespace NetCraft.Network.Protocol.Common;

//CommonPacketTypes common 包类型注册 对应原版 net.minecraft.network.protocol.common.CommonPacketTypes
//id 沿用 Play 协议空间的编号 供 GameProtocols 用 AddPacketCommon 桥接进 Play
//其他协议的 common 包 id 空间不同(如 configuration 从 0 起) 各自在自己的 PacketTypes 里另建一套
public static class CommonPacketTypes
{
    //Clientbound (Play, Clientbound)

    //ClientboundCustomPayload 自定义载荷包
    public static readonly PacketType<ClientCommonPacketListener> ClientboundCustomPayload =
        CreateClientbound<ClientCommonPacketListener>(24, "custom_payload");

    //ClientboundDisconnect 连接断开包
    public static readonly PacketType<ClientCommonPacketListener> ClientboundDisconnect =
        CreateClientbound<ClientCommonPacketListener>(32, "disconnect");

    //ClientboundKeepAlive 心跳包
    public static readonly PacketType<ClientCommonPacketListener> ClientboundKeepAlive =
        CreateClientbound<ClientCommonPacketListener>(44, "keep_alive");

    //ClientboundPing 延迟探测包
    public static readonly PacketType<ClientCommonPacketListener> ClientboundPing =
        CreateClientbound<ClientCommonPacketListener>(61, "ping");

    //ClientboundResourcePackPop 资源包弹出
    public static readonly PacketType<ClientCommonPacketListener> ClientboundResourcePackPop =
        CreateClientbound<ClientCommonPacketListener>(80, "resource_pack_pop");

    //ClientboundResourcePackPush 资源包推送
    public static readonly PacketType<ClientCommonPacketListener> ClientboundResourcePackPush =
        CreateClientbound<ClientCommonPacketListener>(81, "resource_pack_push");

    //ClientboundStoreCookie 存储 cookie
    public static readonly PacketType<ClientCommonPacketListener> ClientboundStoreCookie =
        CreateClientbound<ClientCommonPacketListener>(120, "store_cookie");

    //ClientboundTransfer 转移连接到别的服务器
    public static readonly PacketType<ClientCommonPacketListener> ClientboundTransfer =
        CreateClientbound<ClientCommonPacketListener>(129, "transfer");

    //ClientboundUpdateTags 标签同步
    public static readonly PacketType<ClientCommonPacketListener> ClientboundUpdateTags =
        CreateClientbound<ClientCommonPacketListener>(134, "update_tags");

    //ClientboundCustomReportDetails 自定义举报详情
    public static readonly PacketType<ClientCommonPacketListener> ClientboundCustomReportDetails =
        CreateClientbound<ClientCommonPacketListener>(136, "custom_report_details");

    //ClientboundServerLinks 服务器链接
    public static readonly PacketType<ClientCommonPacketListener> ClientboundServerLinks =
        CreateClientbound<ClientCommonPacketListener>(137, "server_links");

    //ClientboundClearDialog 关闭对话框
    public static readonly PacketType<ClientCommonPacketListener> ClientboundClearDialog =
        CreateClientbound<ClientCommonPacketListener>(139, "clear_dialog");

    //ClientboundShowDialog 显示对话框
    public static readonly PacketType<ClientCommonPacketListener> ClientboundShowDialog =
        CreateClientbound<ClientCommonPacketListener>(140, "show_dialog");

    //Serverbound (Play, Serverbound)

    //ServerboundClientInformation 客户端信息
    public static readonly PacketType<ServerCommonPacketListener> ServerboundClientInformation =
        CreateServerbound<ServerCommonPacketListener>(14, "client_information");

    //ServerboundCustomPayload 自定义载荷
    public static readonly PacketType<ServerCommonPacketListener> ServerboundCustomPayload =
        CreateServerbound<ServerCommonPacketListener>(22, "custom_payload");

    //ServerboundKeepAlive 心跳回包
    public static readonly PacketType<ServerCommonPacketListener> ServerboundKeepAlive =
        CreateServerbound<ServerCommonPacketListener>(28, "keep_alive");

    //ServerboundPong 延迟探测回包
    public static readonly PacketType<ServerCommonPacketListener> ServerboundPong =
        CreateServerbound<ServerCommonPacketListener>(45, "pong");

    //ServerboundResourcePack 资源包状态回执
    public static readonly PacketType<ServerCommonPacketListener> ServerboundResourcePack =
        CreateServerbound<ServerCommonPacketListener>(49, "resource_pack");

    //ServerboundCustomClickAction 自定义点击动作
    public static readonly PacketType<ServerCommonPacketListener> ServerboundCustomClickAction =
        CreateServerbound<ServerCommonPacketListener>(68, "custom_click_action");

    //CreateClientbound 注册 clientbound 包类型 id 为网络 ID
    private static PacketType<THandler> CreateClientbound<THandler>(int id, string identifier)
        where THandler : class
        => PacketTypeRegistry.Register<THandler>(id, ConnectionProtocol.Play, FlowDirection.Clientbound)
            .WithIdentifier(Identifier.WithDefaultNamespace(identifier));

    //CreateServerbound 注册 serverbound 包类型
    private static PacketType<THandler> CreateServerbound<THandler>(int id, string identifier)
        where THandler : class
        => PacketTypeRegistry.Register<THandler>(id, ConnectionProtocol.Play, FlowDirection.Serverbound)
            .WithIdentifier(Identifier.WithDefaultNamespace(identifier));
}
