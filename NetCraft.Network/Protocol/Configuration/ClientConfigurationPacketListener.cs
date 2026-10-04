namespace NetCraft.Network.Protocol.Configuration;

//ClientConfigurationPacketListener 客户端 configuration 监听器
//对应原版 net.minecraft.network.protocol.configuration.ClientConfigurationPacketListener
//继承 ClientCommonPacketListener 加入 6 个 configuration 包的 handle 方法
public interface ClientConfigurationPacketListener : ClientCommonPacketListener
{
    void HandleCodeOfConduct(ClientboundCodeOfConductPacket packet);
    void HandleConfigurationFinished(ClientboundFinishConfigurationPacket packet);
    void HandleRegistryData(ClientboundRegistryDataPacket packet);
    void HandleEnabledFeatures(ClientboundUpdateEnabledFeaturesPacket packet);
    void HandleSelectKnownPacks(ClientboundSelectKnownPacks packet);
    void HandleResetChat(ClientboundResetChatPacket packet);
}
