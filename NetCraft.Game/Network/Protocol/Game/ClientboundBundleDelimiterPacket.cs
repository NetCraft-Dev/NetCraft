namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundBundleDelimiterPacket bundle 分隔符对应原版 ClientboundBundleDelimiterPacket
//标识 bundle 起止边界包实际由 pipeline 处理不应到达 Handle
public sealed class ClientboundBundleDelimiterPacket : BundleDelimiterPacket<ClientGamePacketListener>
{
    public override PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundBundleDelimiter;
}
