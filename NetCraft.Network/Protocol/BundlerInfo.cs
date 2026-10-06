namespace NetCraft.Network.Protocol;

//BundlerInfo bundling info, maps to vanilla net.minecraft.network.protocol.BundlerInfo
//Describes the unbundling and assembly logic for bundle packets
//THandler is the packet handler type; all bundle subpackets inherit Packet<THandler>
public interface BundlerInfo<THandler>
{
    //BundleSizeLimit a single bundle holds at most 4096 subpackets
    public const int BundleSizeLimit = 4096;

    //Bundler subpacket collector
    //AddPacket adds a subpacket; returning null means keep collecting, non-null means the bundle is complete and should be sent
    public interface Bundler<THandler>
    {
        Packet<THandler>? AddPacket(Packet<THandler> packet);
    }

    //UnbundlePacket unbundles a bundle packet into a subpacket sequence output to output
    void UnbundlePacket(Packet<THandler> packet, Action<Packet<THandler>> output);

    //StartPacketBundling starts assembly on receiving the bundle start delimiter and returns a Bundler, otherwise null
    Bundler<THandler>? StartPacketBundling(Packet<THandler> packet);

    //CreateForPacket creates a BundlerInfo for the given bundle packet type
    //bundlePacketType the bundle packet type
    //constructor a factory building a bundle packet from a subpacket sequence
    //delimiterPacket the start and end delimiter packet instance
    static BundlerInfo<THandler> CreateForPacket(
        PacketType<THandler> bundlePacketType,
        Func<IEnumerable<Packet<THandler>>, BundlePacket<THandler>> constructor,
        BundleDelimiterPacket<THandler> delimiterPacket)
        => new ForPacketBundlerInfo<THandler>(constructor, delimiterPacket);
}

//ForPacketBundlerInfo BundlerInfo.CreateForPacket implementation
file sealed class ForPacketBundlerInfo<THandler> : BundlerInfo<THandler>
{
    private readonly Func<IEnumerable<Packet<THandler>>, BundlePacket<THandler>> _constructor;
    private readonly BundleDelimiterPacket<THandler> _delimiterPacket;

    public ForPacketBundlerInfo(
        Func<IEnumerable<Packet<THandler>>, BundlePacket<THandler>> constructor,
        BundleDelimiterPacket<THandler> delimiterPacket)
    {
        _constructor = constructor;
        _delimiterPacket = delimiterPacket;
    }

    public void UnbundlePacket(Packet<THandler> packet, Action<Packet<THandler>> output)
    {
        //bundle packet: output delimiter + subpacket sequence + delimiter
        if (packet is BundlePacket<THandler> bundle)
        {
            output(_delimiterPacket);
            foreach (var sub in bundle.SubPackets())
                output(sub);
            output(_delimiterPacket);
            return;
        }
        output(packet);
    }

    public BundlerInfo<THandler>.Bundler<THandler>? StartPacketBundling(Packet<THandler> packet)
    {
        //Assembly starts a Bundler on receiving the start delimiter packet
        if (ReferenceEquals(packet, _delimiterPacket))
            return new PacketBundler<THandler>(_constructor, _delimiterPacket);
        return null;
    }
}

//PacketBundler collects subpackets and builds the bundle packet when the limit is reached or the end delimiter is received
file sealed class PacketBundler<THandler> : BundlerInfo<THandler>.Bundler<THandler>
{
    private readonly Func<IEnumerable<Packet<THandler>>, BundlePacket<THandler>> _constructor;
    private readonly BundleDelimiterPacket<THandler> _delimiter;
    private readonly List<Packet<THandler>> _bundlePackets = new();

    public PacketBundler(
        Func<IEnumerable<Packet<THandler>>, BundlePacket<THandler>> constructor,
        BundleDelimiterPacket<THandler> delimiter)
    {
        _constructor = constructor;
        _delimiter = delimiter;
    }

    public Packet<THandler>? AddPacket(Packet<THandler> packet)
    {
        //On receiving the end delimiter packet it builds the bundle packet and returns
        if (ReferenceEquals(packet, _delimiter))
            return _constructor(_bundlePackets);
        //Throws when the limit is exceeded
        if (_bundlePackets.Count >= BundlerInfo<THandler>.BundleSizeLimit)
            throw new InvalidOperationException("Bundle subpacket count out of range " + BundlerInfo<THandler>.BundleSizeLimit);
        _bundlePackets.Add(packet);
        return null;
    }
}
