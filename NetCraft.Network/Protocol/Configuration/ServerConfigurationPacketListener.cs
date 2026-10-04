namespace NetCraft.Network.Protocol.Configuration;

//ServerConfigurationPacketListener 服务端 configuration 监听器
//对应原版 net.minecraft.network.protocol.configuration.ServerConfigurationPacketListener
//继承 ServerCommonPacketListener 加入 3 个 configuration 包的 handle 方法
public interface ServerConfigurationPacketListener : ServerCommonPacketListener
{
    void HandleConfigurationFinished(ServerboundFinishConfigurationPacket packet);
    void HandleSelectKnownPacks(ServerboundSelectKnownPacks packet);
    void HandleAcceptCodeOfConduct(ServerboundAcceptCodeOfConductPacket packet);
}
