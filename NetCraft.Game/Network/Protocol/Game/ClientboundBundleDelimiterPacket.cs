namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundBundleDelimiterPacket bundle delimiter, maps to vanilla ClientboundBundleDelimiterPacket
//Marks the bundle start/end boundary; the packet is actually handled by the pipeline and should not reach Handle
public sealed class ClientboundBundleDelimiterPacket : BundleDelimiterPacket<ClientGamePacketListener>
{
    public override PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundBundleDelimiter;
}
