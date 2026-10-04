namespace NetCraft.Network.Protocol.Configuration;

//ConfigurationPacketTypes configuration 阶段所有包类型注册
//对应原版 ConfigurationProtocols 整合 common + cookie + configuration 三类共 30 个包
//按原版 ConfigurationProtocols.CLIENTBOUND_TEMPLATE/SERVERBOUND_TEMPLATE 添加顺序分配 id 0-19/0-9
//common 包暂只注册到 Configuration 协议 Play 阶段需要时另行注册到 Play 协议
//显式指定 Create<THandler> 类型参数避免 C# 推断失败
public static class ConfigurationPacketTypes
{
    //Clientbound (Configuration, Clientbound) 0-19 按原版顺序

    //ClientboundCookieRequest 0 minecraft:cookie_request
    public static readonly PacketType<ClientCookiePacketListener> ClientboundCookieRequest =
        Create<ClientCookiePacketListener>(0, ConnectionProtocol.Configuration, FlowDirection.Clientbound, "cookie_request");

    //ClientboundCustomPayload 1 minecraft:custom_payload
    public static readonly PacketType<ClientCommonPacketListener> ClientboundCustomPayload =
        Create<ClientCommonPacketListener>(1, ConnectionProtocol.Configuration, FlowDirection.Clientbound, "custom_payload");

    //ClientboundDisconnect 2 minecraft:disconnect
    public static readonly PacketType<ClientCommonPacketListener> ClientboundDisconnect =
        Create<ClientCommonPacketListener>(2, ConnectionProtocol.Configuration, FlowDirection.Clientbound, "disconnect");

    //ClientboundFinishConfiguration 3 minecraft:finish_configuration
    public static readonly PacketType<ClientConfigurationPacketListener> ClientboundFinishConfiguration =
        Create<ClientConfigurationPacketListener>(3, ConnectionProtocol.Configuration, FlowDirection.Clientbound, "finish_configuration");

    //ClientboundKeepAlive 4 minecraft:keep_alive
    public static readonly PacketType<ClientCommonPacketListener> ClientboundKeepAlive =
        Create<ClientCommonPacketListener>(4, ConnectionProtocol.Configuration, FlowDirection.Clientbound, "keep_alive");

    //ClientboundPing 5 minecraft:ping
    public static readonly PacketType<ClientCommonPacketListener> ClientboundPing =
        Create<ClientCommonPacketListener>(5, ConnectionProtocol.Configuration, FlowDirection.Clientbound, "ping");

    //ClientboundResetChat 6 minecraft:reset_chat
    public static readonly PacketType<ClientConfigurationPacketListener> ClientboundResetChat =
        Create<ClientConfigurationPacketListener>(6, ConnectionProtocol.Configuration, FlowDirection.Clientbound, "reset_chat");

    //ClientboundRegistryData 7 minecraft:registry_data
    public static readonly PacketType<ClientConfigurationPacketListener> ClientboundRegistryData =
        Create<ClientConfigurationPacketListener>(7, ConnectionProtocol.Configuration, FlowDirection.Clientbound, "registry_data");

    //ClientboundResourcePackPop 8 minecraft:resource_pack_pop
    public static readonly PacketType<ClientCommonPacketListener> ClientboundResourcePackPop =
        Create<ClientCommonPacketListener>(8, ConnectionProtocol.Configuration, FlowDirection.Clientbound, "resource_pack_pop");

    //ClientboundResourcePackPush 9 minecraft:resource_pack_push
    public static readonly PacketType<ClientCommonPacketListener> ClientboundResourcePackPush =
        Create<ClientCommonPacketListener>(9, ConnectionProtocol.Configuration, FlowDirection.Clientbound, "resource_pack_push");

    //ClientboundStoreCookie 10 minecraft:store_cookie
    public static readonly PacketType<ClientCommonPacketListener> ClientboundStoreCookie =
        Create<ClientCommonPacketListener>(10, ConnectionProtocol.Configuration, FlowDirection.Clientbound, "store_cookie");

    //ClientboundTransfer 11 minecraft:transfer
    public static readonly PacketType<ClientCommonPacketListener> ClientboundTransfer =
        Create<ClientCommonPacketListener>(11, ConnectionProtocol.Configuration, FlowDirection.Clientbound, "transfer");

    //ClientboundUpdateEnabledFeatures 12 minecraft:update_enabled_features
    public static readonly PacketType<ClientConfigurationPacketListener> ClientboundUpdateEnabledFeatures =
        Create<ClientConfigurationPacketListener>(12, ConnectionProtocol.Configuration, FlowDirection.Clientbound, "update_enabled_features");

    //ClientboundUpdateTags 13 minecraft:update_tags
    public static readonly PacketType<ClientCommonPacketListener> ClientboundUpdateTags =
        Create<ClientCommonPacketListener>(13, ConnectionProtocol.Configuration, FlowDirection.Clientbound, "update_tags");

    //ClientboundSelectKnownPacks 14 minecraft:select_known_packs
    public static readonly PacketType<ClientConfigurationPacketListener> ClientboundSelectKnownPacks =
        Create<ClientConfigurationPacketListener>(14, ConnectionProtocol.Configuration, FlowDirection.Clientbound, "select_known_packs");

    //ClientboundCustomReportDetails 15 minecraft:custom_report_details
    public static readonly PacketType<ClientCommonPacketListener> ClientboundCustomReportDetails =
        Create<ClientCommonPacketListener>(15, ConnectionProtocol.Configuration, FlowDirection.Clientbound, "custom_report_details");

    //ClientboundServerLinks 16 minecraft:server_links
    public static readonly PacketType<ClientCommonPacketListener> ClientboundServerLinks =
        Create<ClientCommonPacketListener>(16, ConnectionProtocol.Configuration, FlowDirection.Clientbound, "server_links");

    //ClientboundClearDialog 17 minecraft:clear_dialog
    public static readonly PacketType<ClientCommonPacketListener> ClientboundClearDialog =
        Create<ClientCommonPacketListener>(17, ConnectionProtocol.Configuration, FlowDirection.Clientbound, "clear_dialog");

    //ClientboundShowDialog 18 minecraft:show_dialog
    public static readonly PacketType<ClientCommonPacketListener> ClientboundShowDialog =
        Create<ClientCommonPacketListener>(18, ConnectionProtocol.Configuration, FlowDirection.Clientbound, "show_dialog");

    //ClientboundCodeOfConduct 19 minecraft:code_of_conduct
    public static readonly PacketType<ClientConfigurationPacketListener> ClientboundCodeOfConduct =
        Create<ClientConfigurationPacketListener>(19, ConnectionProtocol.Configuration, FlowDirection.Clientbound, "code_of_conduct");

    //Serverbound (Configuration, Serverbound) 0-9 按原版 ConfigurationProtocols.SERVERBOUND_TEMPLATE 顺序

    //ServerboundClientInformation 0 minecraft:client_information
    public static readonly PacketType<ServerCommonPacketListener> ServerboundClientInformation =
        Create<ServerCommonPacketListener>(0, ConnectionProtocol.Configuration, FlowDirection.Serverbound, "client_information");

    //ServerboundCookieResponse 1 minecraft:cookie_response
    public static readonly PacketType<ServerCookiePacketListener> ServerboundCookieResponse =
        Create<ServerCookiePacketListener>(1, ConnectionProtocol.Configuration, FlowDirection.Serverbound, "cookie_response");

    //ServerboundCustomPayload 2 minecraft:custom_payload
    public static readonly PacketType<ServerCommonPacketListener> ServerboundCustomPayload =
        Create<ServerCommonPacketListener>(2, ConnectionProtocol.Configuration, FlowDirection.Serverbound, "custom_payload");

    //ServerboundFinishConfiguration 3 minecraft:finish_configuration
    public static readonly PacketType<ServerConfigurationPacketListener> ServerboundFinishConfiguration =
        Create<ServerConfigurationPacketListener>(3, ConnectionProtocol.Configuration, FlowDirection.Serverbound, "finish_configuration");

    //ServerboundKeepAlive 4 minecraft:keep_alive
    public static readonly PacketType<ServerCommonPacketListener> ServerboundKeepAlive =
        Create<ServerCommonPacketListener>(4, ConnectionProtocol.Configuration, FlowDirection.Serverbound, "keep_alive");

    //ServerboundPong 5 minecraft:pong
    public static readonly PacketType<ServerCommonPacketListener> ServerboundPong =
        Create<ServerCommonPacketListener>(5, ConnectionProtocol.Configuration, FlowDirection.Serverbound, "pong");

    //ServerboundResourcePack 6 minecraft:resource_pack
    public static readonly PacketType<ServerCommonPacketListener> ServerboundResourcePack =
        Create<ServerCommonPacketListener>(6, ConnectionProtocol.Configuration, FlowDirection.Serverbound, "resource_pack");

    //ServerboundSelectKnownPacks 7 minecraft:select_known_packs
    public static readonly PacketType<ServerConfigurationPacketListener> ServerboundSelectKnownPacks =
        Create<ServerConfigurationPacketListener>(7, ConnectionProtocol.Configuration, FlowDirection.Serverbound, "select_known_packs");

    //ServerboundCustomClickAction 8 minecraft:custom_click_action
    public static readonly PacketType<ServerCommonPacketListener> ServerboundCustomClickAction =
        Create<ServerCommonPacketListener>(8, ConnectionProtocol.Configuration, FlowDirection.Serverbound, "custom_click_action");

    //ServerboundAcceptCodeOfConduct 9 minecraft:accept_code_of_conduct
    public static readonly PacketType<ServerConfigurationPacketListener> ServerboundAcceptCodeOfConduct =
        Create<ServerConfigurationPacketListener>(9, ConnectionProtocol.Configuration, FlowDirection.Serverbound, "accept_code_of_conduct");

    private static PacketType<THandler> Create<THandler>(int id, ConnectionProtocol protocol, FlowDirection direction, string identifier)
        where THandler : class
        => PacketTypeRegistry.Register<THandler>(id, protocol, direction)
            .WithIdentifier(Identifier.WithDefaultNamespace(identifier));
}
