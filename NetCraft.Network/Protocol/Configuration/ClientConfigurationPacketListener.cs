namespace NetCraft.Network.Protocol.Configuration;

//ClientConfigurationPacketListener client configuration listener
//Maps to vanilla net.minecraft.network.protocol.configuration.ClientConfigurationPacketListener
//Inherits ClientCommonPacketListener and adds the 6 handle methods for configuration packets
public interface ClientConfigurationPacketListener : ClientCommonPacketListener
{
    void HandleCodeOfConduct(ClientboundCodeOfConductPacket packet);
    void HandleConfigurationFinished(ClientboundFinishConfigurationPacket packet);
    void HandleRegistryData(ClientboundRegistryDataPacket packet);
    void HandleEnabledFeatures(ClientboundUpdateEnabledFeaturesPacket packet);
    void HandleSelectKnownPacks(ClientboundSelectKnownPacks packet);
    void HandleResetChat(ClientboundResetChatPacket packet);
}
