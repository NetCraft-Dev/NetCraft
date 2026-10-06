namespace NetCraft.Network.Protocol.Configuration;

//ServerboundAcceptCodeOfConductPacket the client confirms acceptance of the server's code of conduct
//Maps to vanilla net.minecraft.network.protocol.configuration.ServerboundAcceptCodeOfConductPacket
//No payload, using the INSTANCE singleton
public sealed record ServerboundAcceptCodeOfConductPacket : Packet<ServerConfigurationPacketListener>
{
    public static readonly ServerboundAcceptCodeOfConductPacket Instance = new();

    public static StreamCodec<FriendlyByteBuf, ServerboundAcceptCodeOfConductPacket> StreamCodec { get; }
        = new UnitStreamCodec<FriendlyByteBuf, ServerboundAcceptCodeOfConductPacket>(Instance);

    private ServerboundAcceptCodeOfConductPacket() { }

    public PacketType<ServerConfigurationPacketListener> Type => ConfigurationPacketTypes.ServerboundAcceptCodeOfConduct;

    public void Handle(ServerConfigurationPacketListener handler) => handler.HandleAcceptCodeOfConduct(this);
}
