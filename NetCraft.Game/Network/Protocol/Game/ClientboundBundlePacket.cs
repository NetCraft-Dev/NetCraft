namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundBundlePacket bundle 包打包对应原版 ClientboundBundlePacket
//把多个小包合成一个 bundle 传输降低帧开销继承 BundlePacket
public sealed class ClientboundBundlePacket : BundlePacket<ClientGamePacketListener>
{
    public ClientboundBundlePacket(IEnumerable<Packet<ClientGamePacketListener>> packets) : base(packets) { }

    public override PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundBundle;

    public override void Handle(ClientGamePacketListener handler) => handler.HandleBundlePacket(this);
}
