namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundBundlePacket bundled packet, maps to vanilla ClientboundBundlePacket
//Combines several small packets into one bundle to cut per-frame overhead, extends BundlePacket
public sealed class ClientboundBundlePacket : BundlePacket<ClientGamePacketListener>
{
    public ClientboundBundlePacket(IEnumerable<Packet<ClientGamePacketListener>> packets) : base(packets) { }

    public override PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundBundle;

    public override void Handle(ClientGamePacketListener handler) => handler.HandleBundlePacket(this);
}
