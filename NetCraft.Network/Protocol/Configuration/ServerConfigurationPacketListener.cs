namespace NetCraft.Network.Protocol.Configuration;

//ServerConfigurationPacketListener server-side configuration listener
//Maps to vanilla net.minecraft.network.protocol.configuration.ServerConfigurationPacketListener
//Inherits ServerCommonPacketListener and adds the 3 handle methods for configuration packets
public interface ServerConfigurationPacketListener : ServerCommonPacketListener
{
    void HandleConfigurationFinished(ServerboundFinishConfigurationPacket packet);
    void HandleSelectKnownPacks(ServerboundSelectKnownPacks packet);
    void HandleAcceptCodeOfConduct(ServerboundAcceptCodeOfConductPacket packet);
}
