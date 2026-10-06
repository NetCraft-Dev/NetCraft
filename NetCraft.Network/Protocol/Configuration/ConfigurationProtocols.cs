namespace NetCraft.Network.Protocol.Configuration;

//ConfigurationProtocols configuration protocol registration
//Maps to vanilla net.minecraft.network.protocol.configuration.ConfigurationProtocols
//Registers the 3 Serverbound packets that ServerConfigurationPacketListener directly corresponds to, and
//besides the 6 Clientbound packets that ClientConfigurationPacketListener directly corresponds to
//common/cookie subprotocol packets are registered via AddPacketCommon bridging to ensure a real client can decode them during the config phase
public static class ConfigurationProtocols
{
    //ServerboundTemplate SERVERBOUND configuration protocol template
    //IDs are explicitly defined by ConfigurationPacketTypes in vanilla order; registration order does not affect coding
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

    //Serverbound the bound SERVERBOUND ProtocolInfo
    public static readonly ProtocolInfo<ServerConfigurationPacketListener> Serverbound =
        ServerboundTemplate.Bind();

    //ClientboundTemplate CLIENTBOUND configuration protocol template
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

    //Clientbound the bound CLIENTBOUND ProtocolInfo
    public static readonly ProtocolInfo<ClientConfigurationPacketListener> Clientbound =
        ClientboundTemplate.Bind();
}
