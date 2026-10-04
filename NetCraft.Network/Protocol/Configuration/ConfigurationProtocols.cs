namespace NetCraft.Network.Protocol.Configuration;

//ConfigurationProtocols configuration 协议注册
//对应原版 net.minecraft.network.protocol.configuration.ConfigurationProtocols
//注册 ServerConfigurationPacketListener 直接对应的 3 个 Serverbound 包和
//ClientConfigurationPacketListener 直接对应的 6 个 Clientbound 包外
//common/cookie 子协议包用 AddPacketCommon 桥接注册保证真实客户端 config 阶段可解码
public static class ConfigurationProtocols
{
    //ServerboundTemplate SERVERBOUND configuration 协议模板
    //ID 由 ConfigurationPacketTypes 按原版顺序显式定义 注册顺序无关编解码
    public static readonly SimpleUnboundProtocol<ServerConfigurationPacketListener> ServerboundTemplate =
        new ProtocolInfoBuilder<ServerConfigurationPacketListener>(
            ConnectionProtocol.Configuration, FlowDirection.Serverbound)
            .AddPacketCommon(ConfigurationPacketTypes.ServerboundClientInformation, ServerboundClientInformationPacket.StreamCodec)
            .AddPacketCommon(ConfigurationPacketTypes.ServerboundCookieResponse, ServerboundCookieResponsePacket.StreamCodec)
            .AddPacketCommon(ConfigurationPacketTypes.ServerboundCustomPayload, ServerboundCustomPayloadPacket.StreamCodec)
            .AddPacket(ConfigurationPacketTypes.ServerboundFinishConfiguration, ServerboundFinishConfigurationPacket.StreamCodec)
            .AddPacketCommon(ConfigurationPacketTypes.ServerboundKeepAlive, ServerboundKeepAlivePacket.StreamCodec)
            .AddPacketCommon(ConfigurationPacketTypes.ServerboundPong, ServerboundPongPacket.StreamCodec)
            .AddPacketCommon(ConfigurationPacketTypes.ServerboundResourcePack, ServerboundResourcePackPacket.StreamCodec)
            .AddPacket(ConfigurationPacketTypes.ServerboundSelectKnownPacks, ServerboundSelectKnownPacks.StreamCodec)
            .AddPacketCommon(ConfigurationPacketTypes.ServerboundCustomClickAction, ServerboundCustomClickActionPacket.StreamCodec)
            .AddPacket(ConfigurationPacketTypes.ServerboundAcceptCodeOfConduct, ServerboundAcceptCodeOfConductPacket.StreamCodec)
            .BuildUnbound();

    //Serverbound 绑定后的 SERVERBOUND ProtocolInfo
    public static readonly ProtocolInfo<ServerConfigurationPacketListener> Serverbound =
        ServerboundTemplate.Bind();

    //ClientboundTemplate CLIENTBOUND configuration 协议模板
    public static readonly SimpleUnboundProtocol<ClientConfigurationPacketListener> ClientboundTemplate =
        new ProtocolInfoBuilder<ClientConfigurationPacketListener>(
            ConnectionProtocol.Configuration, FlowDirection.Clientbound)
            .AddPacket(ConfigurationPacketTypes.ClientboundFinishConfiguration, ClientboundFinishConfigurationPacket.StreamCodec)
            .AddPacket(ConfigurationPacketTypes.ClientboundResetChat, ClientboundResetChatPacket.StreamCodec)
            .AddPacket(ConfigurationPacketTypes.ClientboundRegistryData, ClientboundRegistryDataPacket.StreamCodec)
            .AddPacket(ConfigurationPacketTypes.ClientboundUpdateEnabledFeatures, ClientboundUpdateEnabledFeaturesPacket.StreamCodec)
            .AddPacket(ConfigurationPacketTypes.ClientboundSelectKnownPacks, ClientboundSelectKnownPacks.StreamCodec)
            .AddPacket(ConfigurationPacketTypes.ClientboundCodeOfConduct, ClientboundCodeOfConductPacket.StreamCodec)
            .AddPacketCommon(ConfigurationPacketTypes.ClientboundUpdateTags, ClientboundUpdateTagsPacket.StreamCodec)
            .BuildUnbound();

    //Clientbound 绑定后的 CLIENTBOUND ProtocolInfo
    public static readonly ProtocolInfo<ClientConfigurationPacketListener> Clientbound =
        ClientboundTemplate.Bind();
}
